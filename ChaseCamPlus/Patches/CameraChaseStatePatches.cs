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
        ChaseFreeLook.Reset();
        TargetingScreenOverlay.Hide();
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
            UpdateState.ShowMapWidgets(show: true);
            UpdateState.ApplyElementOverrides(inChase: false);

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
    /// Vanilla hides the flight HUD in chase view. <c>EnterState</c> clears <c>showHUD</c>, and
    /// <c>CheckHUD</c> short-circuits on it before ever reaching the branch that would turn the HUD
    /// on; the only thing that sets the flag back is <c>ToggleHUD</c>, reachable solely through a
    /// debug key gated on <c>GameState.Editor</c>. So in an ordinary match third person means no
    /// instruments and no virtual joystick indicator.
    ///
    /// Setting the flag before the method runs hands the decision back to the game's own logic
    /// rather than forcing the canvas on: the HUD appears in the positions it was written for
    /// (Back, Tail, wing roots, Belly) and stays off in the ones where an overlay would be nonsense.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.CheckHUD))]
    static class CheckHUD
    {
        static void Prefix(ref bool ___showHUD)
        {
            if (Plugin.FlightHud.Value)
                ___showHUD = true;
        }
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
            ChaseTargetLook.RestoreBasePose(cam);
            ChaseCameraGeometry.Restore(cam);
            return cam != null && cam.followingUnit != null;
        }

        static void Postfix(CameraStateManager cam, float ___viewDistAdjust,
            Vector3 ___posVector, Vector3 ___targetVector, Quaternion ___cameraCustomRotation)
        {
            if (cam == null || cam.currentState != cam.chaseState)
                return;

            EnforceHudVisibility();
            if (cam.cameraPivot == null || cam.followingUnit == null)
                return;

            ChaseCameraGeometry.Capture(cam);
            float dt = Time.unscaledDeltaTime;
            bool inputAllowed = ChaseInput.IsAllowed(cam);
            bool active = ChaseFreeLook.IsActive();
            float panInput = inputAllowed ? GameManager.playerInput.GetAxis("Pan View") : 0f;
            float tiltInput = inputAllowed ? GameManager.playerInput.GetAxis("Tilt View") : 0f;
            var controller = GameManager.playerInput?.controllers.GetLastActiveController();
            bool usingStick = controller != null && controller.type == Rewired.ControllerType.Joystick;
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
            Vector3 referenceUp = CinematicCamera.OrbitReferenceUp(cam);
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
                _desiredPan += panInput * 120f * sensitivity * dt;
                _desiredTilt += tiltInput * 120f * sensitivity * dt * invert;
                _desiredPan = FreeLookAngles.Wrap(_desiredPan);
                _desiredTilt = FreeLookAngles.ClampTilt(_desiredTilt, baseElevation);
                if (centering == CenteringAction.Pull)
                {
                    _desiredPan = CameraCentering.Decay(_desiredPan, Plugin.CenteringForce.Value, dt);
                    _desiredTilt = CameraCentering.Decay(_desiredTilt, Plugin.CenteringForce.Value, dt);
                }
            }
            else if (inputAllowed && !active && !Plugin.KeepFreeLookAngle.Value)
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
        /// Keeps the HUD in the state the config asks for, checked every frame instead of once on
        /// entry.
        ///
        /// Setting <c>showHUD</c> in <c>CheckHUD</c> is not enough on its own, because that method
        /// only runs when the chase state is entered, while plenty of other code turns the HUD off
        /// afterwards and turns it back on under a cockpit-only condition. Pausing runs
        /// <c>FlightHud.EnableCanvas(false)</c> unconditionally, and the resume path only restores
        /// it <c>if (currentState == cockpitState)</c>. Worse, <c>SwitchState</c> calls
        /// <c>DynamicMap.Minimize()</c> immediately *after* <c>EnterState</c>, and that method ends
        /// with <c>EnableCanvas(aircraft != null &amp;&amp; cameraMode == cockpit)</c> — so the map is
        /// switched off a moment after entering chase had switched it on.
        ///
        /// Rather than chase every one of those paths, and every new one a game update might add,
        /// this repairs the state whenever it finds it wrong. <c>EnableCanvas</c> drags a
        /// <c>RefreshSettings()</c> across five singletons behind it, so it is only called when
        /// something actually needs changing, never once per frame.
        /// </summary>
        private static void EnforceHudVisibility()
        {
            FlightHud hud = SceneSingleton<FlightHud>.i;
            bool hudActive = hud != null && FlightHudCanvasActive(hud);

            // One set of settings covers both chase cameras. They differ in where they point, not
            // in what the HUD is drawn on, and someone who wants instruments in third person wants
            // them in third person.
            if (!Plugin.FlightHud.Value)
            {
                // Vanilla already leaves the HUD off in chase, so there is normally nothing to do —
                // but the mode can be switched on with the HUD already up, and then it has to come
                // down. Only the canvas is touched: the minimap furniture lives under it and goes
                // with it, and its own state is left alone so it returns as the config asks.
                if (hudActive)
                    FlightHud.EnableCanvas(enable: false);

                return;
            }

            if (DynamicMap.mapMaximized)
            {
                // Opening the map full screen hides the flight HUD so it is not painted over the
                // map — but `Maximize()` only does that `if (currentState == cockpitState)`. In
                // chase that branch never runs, so the HUD stays up: a translucent grey overlay with
                // the green velocity vector sitting on top of the map. Do what the cockpit does.
                if (hudActive)
                    FlightHud.EnableCanvas(enable: false);

                // The player asked for the map, so put its widgets back whatever MapInChase says —
                // that flag is about the minimap in chase, not about a map opened deliberately.
                ShowMapWidgets(true);
                return;
            }

            if (hud != null && !hudActive)
                FlightHud.EnableCanvas(enable: true);

            ShowMapWidgets(Plugin.Map.Value);
            ApplyElementOverrides(inChase: true);
        }

        /// <summary>
        /// Shows or hides the minimap and the chrome drawn around it.
        ///
        /// Switching off the <c>DynamicMap</c> object alone leaves the widget's furniture behind: a
        /// dark plate in the lower left with a small forward-reference marker on it, both of which
        /// live outside that object. The plate is the <c>Image</c> on the panel two levels up, and
        /// the marker is a sibling of the map under the anchor between them — so hiding the anchor
        /// takes the marker and the map together, and disabling just that one Image takes the plate.
        ///
        /// The panel itself has to stay active: it also carries the threat list, and switching it
        /// off would cost the player their missile warnings to tidy up an empty frame.
        ///
        /// Objects are reached by walking up from the map rather than by name, so this survives a
        /// renamed hierarchy as long as the shape of it holds.
        /// </summary>
        internal static void ShowMapWidgets(bool show)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null)
                return;

            Transform anchor = map.transform.parent;
            Transform panel = anchor != null ? anchor.parent : null;

            if (anchor != null && anchor.gameObject.activeSelf != show)
                anchor.gameObject.SetActive(show);

            if (show && !map.gameObject.activeSelf)
                DynamicMap.EnableCanvas(enable: true);

            SetPlateVisible(panel, show);
        }

        /// <summary>
        /// Toggles the panel's own Image without touching its children. Matched on type name and
        /// handled as a <see cref="Behaviour"/> so this does not need a reference to the UI assembly
        /// for a single <c>enabled</c> flag.
        /// </summary>
        private static void SetPlateVisible(Transform panel, bool show)
        {
            if (panel == null)
                return;

            foreach (Component component in panel.GetComponents<Component>())
            {
                if (component is Behaviour behaviour
                    && component.GetType().Name == "Image"
                    && behaviour.enabled != show)
                {
                    behaviour.enabled = show;
                }
            }
        }

        /// <summary>
        /// Applies the per-element chase overrides, and clears them again when leaving chase so the
        /// cockpit never inherits a setting that was only ever about third person.
        ///
        /// Note what these two cannot do: the pitch ladder's rungs and its horizon line are one
        /// scrolling texture — the game moves a single <c>RawImage</c>'s uvRect with pitch — so
        /// hiding the ladder takes the horizon line with it. Separating them would mean replacing
        /// the texture, not toggling an object.
        /// </summary>
        internal static void ApplyElementOverrides(bool inChase)
        {
            FlightHud hud = SceneSingleton<FlightHud>.i;
            if (hud == null)
                return;

            bool showLadder = !inChase || Plugin.PitchLadder.Value;
            if (Field(ref _pitchLadderField, "pitchCompassCenter")?.GetValue(hud) is GameObject ladder
                && ladder.activeSelf != showLadder)
            {
                ladder.SetActive(showLadder);
            }

            // The weapon HUD states switch the waterline back on whenever the selected weapon
            // changes, so this is re-applied every frame rather than set once.
            bool showWaterline = !inChase || Plugin.Waterline.Value != HudAttachment.Hidden;
            if (Field(ref _waterlineField, "waterline")?.GetValue(hud) is Behaviour waterline
                && waterline.enabled != showWaterline)
            {
                waterline.enabled = showWaterline;
            }

            // Only the Image is switched off, never the GameObject. PlayerAxisControls keeps the
            // virtual joystick's deflection in virtualJoystickPos.transform.localPosition and reads
            // pitch, roll and yaw straight back out of it, so deactivating the object would take the
            // stick away along with its indicator. The game drives the same object through SetActive
            // and its colour and never touches enabled, so there is nothing here to fight over.
            //
            // Reached by reflection and held as Behaviour rather than Image, like the waterline
            // above: the mod does not reference UnityEngine.UI, and enabled is all it needs.
            bool showStick = !inChase || Plugin.VirtualJoystickIndicator.Value;
            if (Field(ref _joystickPosField, "virtualJoystickPos")?.GetValue(hud) is Behaviour knob
                && knob.enabled != showStick)
            {
                knob.enabled = showStick;
            }

            if (Field(ref _joystickVectorField, "virtualJoystickVector")?.GetValue(hud) is Behaviour vector
                && vector.enabled != showStick)
            {
                vector.enabled = showStick;
            }

            // The instrument groups are whole objects rather than fields on the HUD, and which ones
            // exist depends on the airframe, so they are resolved by component type rather than named
            // here. See HudElements.
            if (inChase)
            {
                HudElements.Apply(
                    Plugin.LeftInstruments.Value,
                    Plugin.RightInstruments.Value,
                    Plugin.Compass.Value);
            }
            else
            {
                HudElements.ShowAll();
            }
        }

        private static FieldInfo _pitchLadderField;
        private static FieldInfo _waterlineField;
        private static FieldInfo _joystickPosField;
        private static FieldInfo _joystickVectorField;

        private static FieldInfo Field(ref FieldInfo cache, string name) =>
            cache ??= AccessTools.Field(typeof(FlightHud), name);

        // FlightHud.canvas is private, and there is no public way to ask whether the HUD is showing.
        private static FieldInfo _canvasField;

        private static bool FlightHudCanvasActive(FlightHud hud)
        {
            _canvasField ??= AccessTools.Field(typeof(FlightHud), "canvas");

            // Typed as Component rather than Canvas: only the GameObject is needed, and naming
            // Canvas would drag in a reference to UnityEngine.UIModule for nothing.
            if (_canvasField?.GetValue(hud) is Component canvas)
                return canvas.gameObject.activeSelf;

            // Cannot tell — assume it is fine rather than hammer EnableCanvas every frame.
            return true;
        }

        /// <summary>
        /// Orbiting can swing the camera through the ground. This repeats the collision pull-in the
        /// chase state does for its own placement, using the same cast and the same layer mask.
        /// </summary>
        private static void KeepOutOfTerrain(CameraStateManager cam, Vector3 pivot) =>
            CameraTerrainAvoidance.Apply(cam, pivot);
    }
}
