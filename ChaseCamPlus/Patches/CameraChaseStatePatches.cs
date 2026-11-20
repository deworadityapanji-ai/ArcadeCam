using System;
using System.Reflection;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Patches;

public class CameraChaseStatePatches
{
    // Where we want to be, and where we currently are. Kept apart so the camera eases into the
    // orbit the same way the rest of the chase state eases, instead of snapping frame to frame.
    private static float _desiredPan;
    private static float _desiredTilt;
    private static float _pan;
    private static float _tilt;
    private static bool _wasFreeLookActive;
    private static bool _wasLookingAtTarget;
    private static bool _recenterRequested;
    private static bool _rebaseLook;
    private static bool _hasLookDirections;
    private static CameraStateManager _lookCamera;
    private static Unit _lookUnit;
    private static Vector3 _lastLookDirection, _lastDesiredDirection;

    internal static void PreserveLookForHorizonChange()
    {
        if (!_hasLookDirections) return;
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam != null && cam == _lookCamera
            && cam.currentState == cam.chaseState && cam.followingUnit == _lookUnit)
            _rebaseLook = true;
    }
    private static readonly CenteringInputState CenteringInput = new CenteringInputState();

    internal static void RecenterView()
    {
        ChaseViewReturn.Begin(SceneSingleton<CameraStateManager>.i);
        ChaseTargetLook.RestoreBasePose(SceneSingleton<CameraStateManager>.i);
        ChaseTargetLook.CancelUntilReleased();
        _recenterRequested = true;
    }

    private static void ResetOrbit()
    {
        _desiredPan = 0f;
        _desiredTilt = 0f;
        _pan = 0f;
        _tilt = 0f;
        _wasFreeLookActive = false;
        _wasLookingAtTarget = false;
        _recenterRequested = false;
        _rebaseLook = false;
        _hasLookDirections = false;
        _lookCamera = null; _lookUnit = null;
        CenteringInput.Reset();
        ChaseViewReturn.Reset();
        ChaseCameraGeometry.Reset();
        ChaseCameraMotion.Reset();
        DynamicChaseCamera.Reset();
        ChasePivotSmoothing.Reset();
        MouseAimView.Reset();
        ChaseFreeLook.Reset();
        ChaseTargetLook.Reset();
    }

    /// <summary>Start every chase session from the default angle.</summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.EnterState))]
    static class EnterState
    {
        static void Postfix(CameraStateManager cam)
        {
            if (cam == null || cam.currentState != cam.chaseState) return;
            ResetOrbit();
            ChaseFreeLook.EnterChase();
            CinematicCamera.SetEnabled(Plugin.LevelHorizonByDefault.Value);
            CinematicCamera.Reset();
        }
    }

    /// <summary>
    /// Hiding the minimap widgets is a chase-only decision, so leaving chase has to put them back —
    /// otherwise the cockpit inherits a missing map from a setting that was never about it.
    ///
    /// A finalizer rather than a postfix, because vanilla <c>LeaveState</c> can throw and a postfix
    /// would then never run — leaving the HUD elements hidden into the next life. Its last statement
    /// is <c>cam.followingUnit.SetDoppler(true)</c> with no null check, and the way this state is
    /// usually left is <c>SetFollowingUnit</c> clearing that very field after the pilot dies. A
    /// finalizer runs either way.
    ///
    /// The exception is swallowed only in that exact case, so anything else still surfaces. Nothing
    /// is lost by swallowing it: every statement that matters — unparenting the pivot, restoring the
    /// shadow distance, unsubscribing from the cockpit — has already run by then, and the one that
    /// did not was about to talk to a unit that no longer exists.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.LeaveState))]
    static class LeaveState
    {
        static Exception Finalizer(Exception __exception, CameraStateManager cam)
        {
            ResetOrbit();

            if (__exception is NullReferenceException && cam != null && cam.followingUnit == null)
            {
                if (!_reportedLeaveStateBug)
                {
                    _reportedLeaveStateBug = true;
                    Plugin.Logger.LogInfo(
                        "Suppressed a vanilla NullReferenceException in CameraChaseState.LeaveState: "
                        + "it ends by dereferencing followingUnit, which SetFollowingUnit has already "
                        + "cleared on the path that gets here. Reported once per session.");
                }

                return null;
            }

            return __exception;
        }

        private static bool _reportedLeaveStateBug;
    }

    /// <summary>
    /// Vanilla <c>CameraChaseState</c> never reads "Pan View" / "Tilt View" / "Free Look" — chase is
    /// the only camera state in the game with no free look at all. Rather than reproduce the state's
    /// positioning maths, this runs after it and rotates the finished camera around the aircraft, so
    /// it composes with the numpad view presets, the zoom, and the existing smoothing.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.UpdateState))]
    static class UpdateState
    {
        /// <summary>
        /// Skips the vanilla body once the followed unit has gone.
        ///
        /// <c>UpdateState</c> guards on <c>followingRB</c> being null but not on <c>followingUnit</c>,
        /// and the very next thing it does is call <c>CheckInput</c>, which opens with
        /// <c>cam.followingUnit.definition</c>. After the pilot dies, <c>SetFollowingUnit</c> clears
        /// that field while the chase state is still the live one, and the method then throws every
        /// frame until something else moves the camera — several hundred times in a row, with the
        /// camera never positioned in between.
        ///
        /// Nothing is lost by skipping: the frames this replaces were frames that threw before
        /// reaching any of the placement code. The postfix below still runs, and guards the same
        /// field itself.
        /// </summary>
        static bool Prefix(CameraStateManager cam)
        {
            WeaponCamera.BeforeCameraUpdate(cam);
            if (WtMouseAimCompatibility.OwnsChase(cam) && _wasFreeLookActive
                && !ChaseFreeLook.IsActive() && !WtMouseAimCompatibility.NativeFreeLookHeld)
                ChaseViewReturn.Begin(cam);
            ChaseTargetLook.RestoreBasePose(cam);
            ChaseCameraGeometry.Restore(cam);
            return cam != null && cam.followingUnit != null;
        }

        static void Postfix(CameraStateManager cam, float ___viewDistAdjust,
            Vector3 ___posVector, Vector3 ___targetVector, Quaternion ___cameraCustomRotation,
            ref Quaternion ___cameraRotationPrev)
        {
            if (cam == null || cam.currentState != cam.chaseState)
                return;

            if (cam.cameraPivot == null || cam.followingUnit == null)
                return;

            // Frame-rate independent replacement for the game's pivot rotation smoothing.
            ChasePivotSmoothing.Apply(cam, ___targetVector, ref ___cameraRotationPrev);

            ChaseCameraGeometry.Capture(cam);
            float dt = Time.unscaledDeltaTime;
            bool inputAllowed = ChaseInput.IsAllowed(cam);
            bool wtMouseAim = WtMouseAimCompatibility.OwnsChase(cam);
            bool active = ChaseFreeLook.IsActive() || (wtMouseAim && WtMouseAimCompatibility.NativeFreeLookHeld);
            float panInput = inputAllowed ? GameManager.playerInput.GetAxis("Pan View") : 0f;
            float tiltInput = inputAllowed ? GameManager.playerInput.GetAxis("Tilt View") : 0f;
            var controller = GameManager.playerInput?.controllers.GetLastActiveController();
            bool usingStick = controller != null && controller.type == Rewired.ControllerType.Joystick;
            // WT captures mouse input outside Rewired, so its raw deltas may
            // leave Rewired's last-controller type at Joystick. A live look
            // stick still wins; an idle stick must not block mouse free look.
            float stickLookDeadzone = Mathf.Max(0.0001f, Plugin.CenteringDeadzone.Value);
            bool stickLook = usingStick && (Mathf.Abs(panInput) >= stickLookDeadzone
                || Mathf.Abs(tiltInput) >= stickLookDeadzone);
            bool wtMouseLook = wtMouseAim && !stickLook;
            if (wtMouseLook)
            {
                Vector2 step = WtMouseAimCompatibility.FreeLookStep();
                panInput = step.x; tiltInput = step.y;
            }
            float deadzone = usingStick ? Plugin.CenteringDeadzone.Value : 0.0001f;
            if (Mathf.Abs(panInput) < deadzone) panInput = 0f;
            if (Mathf.Abs(tiltInput) < deadzone) tiltInput = 0f;
            bool movingLook = panInput != 0f || tiltInput != 0f;
            bool lookingAtTarget = ChaseTargetLook.TryGetTargetPosition(cam, out Vector3 targetPosition);
            CenteringAction centering = CenteringInput.Read(Plugin.CenteringMode.Value, active,
                lookingAtTarget || ChaseTargetLook.IsTransitioning || ChaseViewReturn.IsActive
                    || _rebaseLook || CinematicCamera.IsTransitioning, movingLook,
                Plugin.CenteringEnabled.Value ? Plugin.CenteringForce.Value : 0f, Plugin.CenteringDelay.Value, dt);
            bool targetEnded = _wasLookingAtTarget && !lookingAtTarget;
            bool recentered = _recenterRequested || (targetEnded && Plugin.CenterAfterTargetLook.Value)
                              || centering == CenteringAction.Snap;
            if (recentered)
            {
                _desiredPan = _pan = 0f;
                _desiredTilt = _tilt = 0f;
                _wasFreeLookActive = false;
                Transform aircraft = cam.followingUnit.transform;
                Vector3 presetPosition = ___posVector;
                Vector3 presetDirection = ___targetVector;
                Vector3 presetOffset = aircraft.right * presetPosition.x
                                       + aircraft.up * presetPosition.y
                                       + aircraft.forward * presetPosition.z;
                cam.transform.position = cam.cameraPivot.position + presetOffset * (1f + ___viewDistAdjust);
                Vector3 presetForward = aircraft.right * presetDirection.x
                                        + aircraft.up * presetDirection.y
                                        + aircraft.forward * presetDirection.z;
                if (presetForward.sqrMagnitude > 0.0001f)
                    cam.transform.rotation = Quaternion.LookRotation(presetForward, aircraft.up) * ___cameraCustomRotation;
                KeepOutOfTerrain(cam, cam.cameraPivot.position);
                ChaseCameraGeometry.Capture(cam);
                CinematicCamera.Recenter();
                // An explicit Center press suppresses a held target button until release.
                if (!_recenterRequested && !targetEnded)
                    ChaseTargetLook.Reset();
                _recenterRequested = false;
                _rebaseLook = false;
                _hasLookDirections = false;
                _lookCamera = null; _lookUnit = null;
            }
            _wasLookingAtTarget = lookingAtTarget;

            if (CinematicCamera.Apply(cam, ___viewDistAdjust))
                KeepOutOfTerrain(cam, cam.cameraPivot.position);

            ChaseCameraGeometry.Apply(cam);
            ChaseCameraMotion.Apply(cam, movingLook || Mathf.Abs(_pan) > 1f || Mathf.Abs(_tilt) > 1f,
                lookingAtTarget || ChaseTargetLook.IsTransitioning || ChaseViewReturn.IsActive);
            // Leans the camera into manoeuvres. Free look keeps it: the orbit below swings the
            // already-moved camera, so a held look angle still feels the aircraft's motion. Target
            // look and the return from it re-aim the camera, so those pause it. With WT Mouse Aim,
            // WT's aim view keeps this offset because it preserves the camera's relative framing.
            DynamicChaseCamera.Apply(cam,
                lookingAtTarget || ChaseTargetLook.IsTransitioning || ChaseViewReturn.IsActive);
            MouseAimView.Apply(cam);
            Vector3 referenceUp = wtMouseAim ? MouseAimView.ReferenceUp : CinematicCamera.OrbitReferenceUp(cam);
            Vector3 orbitUp = FreeLookAngles.YawAxis(cam.transform.forward, cam.transform.up, referenceUp);
            Vector3 baseForward = cam.transform.forward;
            float baseElevation = FreeLookAngles.Elevation(baseForward, orbitUp);
            if (_rebaseLook && _hasLookDirections)
            {
                Vector2 current = FreeLookAngles.Rebase(_lastLookDirection, cam.transform.forward, orbitUp);
                Vector2 desired = FreeLookAngles.Rebase(_lastDesiredDirection, cam.transform.forward, orbitUp);
                _pan = current.x; _tilt = current.y;
                _desiredPan = desired.x; _desiredTilt = desired.y;
                _rebaseLook = CinematicCamera.IsTransitioning;
            }
            if (lookingAtTarget)
            {
                _desiredPan = _pan;
                _desiredTilt = _tilt;
            }
            else if (active && !recentered)
            {
                float invert = PlayerSettings.viewInvertPitch ? -1f : 1f;
                float sensitivity = PlayerSettings.viewSensitivity * Plugin.FreeLookSensitivity.Value;
                _desiredPan += wtMouseLook ? panInput * Plugin.FreeLookSensitivity.Value : panInput * 120f * sensitivity * dt;
                _desiredTilt += wtMouseLook ? tiltInput * Plugin.FreeLookSensitivity.Value : tiltInput * 120f * sensitivity * dt * invert;
                _desiredPan = FreeLookAngles.Wrap(_desiredPan);
                _desiredTilt = FreeLookAngles.ClampTilt(_desiredTilt, baseElevation);
                if (centering == CenteringAction.Pull)
                {
                    _desiredPan = CameraCentering.Decay(_desiredPan, Plugin.CenteringForce.Value, dt);
                    _desiredTilt = CameraCentering.Decay(_desiredTilt, Plugin.CenteringForce.Value, dt);
                }
            }
            else if (inputAllowed && !active && (wtMouseAim || !Plugin.KeepFreeLookAngle.Value))
            {
                _desiredPan = 0f;
                _desiredTilt = 0f;
            }
            else if (inputAllowed && !active && _wasFreeLookActive)
            {
                _desiredPan = _pan;
                _desiredTilt = _tilt;
            }
            if (inputAllowed) _wasFreeLookActive = active;

            float t = Mathf.Min(5f * dt / Mathf.Max(PlayerSettings.viewSmoothing, 0.01f), 1f);
            _pan = FreeLookAngles.Step(_pan, _desiredPan, t);
            _desiredTilt = FreeLookAngles.ClampTilt(_desiredTilt, baseElevation);
            _tilt = FreeLookAngles.StepTilt(_tilt, _desiredTilt, t, baseElevation);

            Vector3 pivot = cam.cameraPivot.position;
            Vector3 offset = cam.transform.position - pivot;
            if ((Mathf.Abs(_pan) >= 0.01f || Mathf.Abs(_tilt) >= 0.01f)
                && offset.sqrMagnitude >= 0.0001f)
            {
                // Build an upright orbit frame before pitching. The limit includes the
                // native camera elevation, so a raised/tilted rig cannot cross a pole.
                Quaternion basis = Quaternion.LookRotation(cam.transform.forward, orbitUp);
                Quaternion orbit = Quaternion.AngleAxis(_pan, orbitUp)
                                   * Quaternion.AngleAxis(_tilt, basis * Vector3.right);
                cam.transform.position = pivot + orbit * offset;
                cam.transform.rotation = orbit * basis;
                KeepOutOfTerrain(cam, pivot);
            }

            // Remember manual directions before target tracking/centering overlays.
            _lastLookDirection = cam.transform.forward;
            _lastDesiredDirection = FreeLookAngles.Direction(_desiredPan, _desiredTilt,
                // The orbit was applied to this frame's native base forward.
                baseForward, orbitUp);
            _lookCamera = cam; _lookUnit = cam.followingUnit; _hasLookDirections = true;
            ChaseTargetLook.Apply(cam, lookingAtTarget, targetPosition);
            ChaseViewReturn.Apply(cam);
        }
        /// <summary>
        /// Orbiting can swing the camera through the ground. This repeats the collision pull-in the
        /// chase state does for its own placement, using the same cast and the same layer mask.
        /// </summary>
        private static void KeepOutOfTerrain(CameraStateManager cam, Vector3 pivot) =>
            CameraTerrainAvoidance.Apply(cam, pivot);
    }
}
