using UnityEngine;

namespace ArcadeCam.Helpers;

/// <summary>
/// Layers spring-damped offsets on top of the normal chase pose, driven by how fast the aircraft
/// is rotating. Runs after the boom/pitch motion and before free look, so free look orbits the
/// already-moved camera. Not used with the level-horizon cameras, which have their own motion.
/// </summary>
internal static class DynamicChaseCamera
{
    private static readonly DynamicCameraState State = new DynamicCameraState();
    private static Unit _unit;
    // Last frame's inputs/outputs, for the twitch probe.
    private static Vector3 _lastRates;
    private static bool _lastEnabled, _lastApplied;
    private static float _lastFixedTime = -1f;

    internal static void Reset() { State.Reset(); _unit = null; _lastFixedTime = -1f; }

    /// <summary>
    /// How far to advance the spring this frame. If the aircraft's rigidbody is not interpolated,
    /// it only moves on physics steps; at a frame rate above the physics rate some frames show it
    /// unmoved. A spring advanced every frame would keep moving the camera on those frames and make
    /// the aircraft visibly step against it. So advance only when a physics step has happened, by
    /// the physics time that passed: camera and aircraft then move together.
    /// </summary>
    private static float StepTime(Rigidbody rb)
    {
        if (rb.interpolation != RigidbodyInterpolation.None) { _lastFixedTime = -1f; return Time.deltaTime; }
        float now = Time.fixedTime;
        float dt = _lastFixedTime < 0f ? Time.fixedDeltaTime : now - _lastFixedTime;
        _lastFixedTime = now;
        return dt < 0f || dt > 0.25f ? Time.fixedDeltaTime : dt;   // 0 = no physics step this frame: hold
    }

    /// <param name="suspended">True while the player is actually looking around, tracking a target or returning.</param>
    internal static void Apply(CameraStateManager cam, bool suspended)
    {
        if (cam.followingRB == null || cam.followingUnit == null) { Reset(); return; }
        _baseView = View(cam); _hasBaseView = true;   // TEMPORARY probe: the camera before dynamic chase
        if (_unit != cam.followingUnit) { State.Reset(); _unit = cam.followingUnit; _lastFixedTime = -1f; }
        bool enabled = Plugin.DynamicChaseEnabled.Value && !suspended && !CinematicCamera.Active;
        _lastEnabled = enabled; _lastApplied = false;
        if (!enabled && State.Weight <= 0.0001f) { State.Reset(); return; }

        Transform aircraft = cam.followingUnit.transform;
        Vector3 rates = aircraft.InverseTransformDirection(cam.followingRB.angularVelocity) * Mathf.Rad2Deg;
        _lastRates = rates; _lastApplied = true;
        State.Advance(rates.x, rates.y, rates.z, enabled,
            Plugin.DynamicChaseStrength.Value, Plugin.DynamicChaseRollLag.Value,
            Plugin.DynamicChaseDeadzone.Value, Plugin.DynamicChaseResponse.Value, StepTime(cam.followingRB));

        float weight = State.Weight;
        if (weight <= 0.0001f) return;
        Vector3 pivot = cam.cameraPivot.position;
        float boom = (cam.transform.position - pivot).magnitude;
        // Screen-space axes, so "up" is always up on the screen.
        cam.transform.position += (cam.transform.right * State.Side + cam.transform.up * State.Lift) * (boom * weight);
        if (Mathf.Abs(State.RollLag) > 0.001f)
            cam.transform.rotation = Quaternion.AngleAxis(State.RollLag * weight, cam.transform.forward) * cam.transform.rotation;
        CameraTerrainAvoidance.Apply(cam, pivot);
    }

    // ---- TEMPORARY twitch probe (remove before release) -----------------------------------
    // After the whole chase update, watches where the aircraft sits on screen and the camera's
    // roll relative to it. A smooth camera changes these smoothly; a twitch is a sudden change in
    // that rate. Each one is logged as "[Twitch]" with the dynamic camera's state for that frame,
    // whether dynamic chase is on or off, so the two can be compared.
    private static Vector3 _prevView, _prevStep, _prevPivot, _baseView, _prevBaseView, _prevBaseStep;
    private static bool _hasView, _hasStep, _hasBaseView, _hasBaseStep;
    private static float _prevDt;
    private static int _lastProbeFrame, _updatesThisFrame;

    // Horizontal and vertical screen angle of the aircraft, and camera roll relative to it.
    private static Vector3 View(CameraStateManager cam)
    {
        Vector3 local = cam.transform.InverseTransformPoint(cam.cameraPivot.position);
        Vector3 aircraftUp = Vector3.ProjectOnPlane(cam.followingUnit.transform.up, cam.transform.forward);
        return new Vector3(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg,
            Vector3.SignedAngle(cam.transform.up, aircraftUp, cam.transform.forward));
    }

    private static Vector3 Step(Vector3 now, Vector3 before)
    {
        Vector3 step = now - before;
        step.z = Mathf.DeltaAngle(0f, step.z);
        return step;
    }
    private static float _nextProbeLog;
    private static int _probeSkipped;
    private static RigidbodyInterpolation _loggedInterpolation = (RigidbodyInterpolation)(-1);

    internal static void Probe(CameraStateManager cam)
    {
        if (cam == null || cam.cameraPivot == null || cam.followingUnit == null || cam.followingRB == null) { _hasView = _hasStep = _hasBaseStep = false; return; }
        // Count chase updates per rendered frame (more than one would double-step the smoothing).
        if (Time.frameCount == _lastProbeFrame) _updatesThisFrame++; else { _lastProbeFrame = Time.frameCount; _updatesThisFrame = 1; }
        if (_updatesThisFrame > 1) Plugin.Logger.LogInfo($"[Twitch] chase camera updated {_updatesThisFrame}x in frame {Time.frameCount}");
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.2f) { _hasView = _hasStep = _hasBaseStep = false; return; }
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 view = View(cam);
        // Screen angles are relative, so an origin shift does not disturb them: measure those frames too.
        bool originShift = _hasView && (pivot - _prevPivot).magnitude > cam.followingRB.velocity.magnitude * dt + 50f;
        Vector3 baseJerk = Vector3.zero;
        if (_hasBaseView && _hasView)
        {
            Vector3 baseStep = Step(_baseView, _prevBaseView);
            if (_hasBaseStep) baseJerk = baseStep - _prevBaseStep;
            _prevBaseStep = baseStep; _hasBaseStep = true;
        }
        if (_hasBaseView) _prevBaseView = _baseView;
        _hasBaseView = false;
        if (_hasView)
        {
            Vector3 step = Step(view, _prevView);
            if (_hasStep)
            {
                Vector3 jerk = step - _prevStep;
                if (Mathf.Abs(jerk.x) > 0.6f || Mathf.Abs(jerk.y) > 0.6f || Mathf.Abs(jerk.z) > 1.2f)
                {
                    if (Time.unscaledTime >= _nextProbeLog)
                    {
                        Plugin.Logger.LogInfo($"[Twitch] t={Time.unscaledTime:F2}s frame={Time.frameCount} dt={dt * 1000f:F1}ms (prev {_prevDt * 1000f:F1}ms)"
                            + (originShift ? " ORIGIN-SHIFT FRAME" : "")
                            + $" screen jump h={jerk.x:F2} v={jerk.y:F2} roll={jerk.z:F2} deg, base camera h={baseJerk.x:F2} v={baseJerk.y:F2} roll={baseJerk.z:F2}"
                            + $" | wt={WtMouseAimCompatibility.OwnsChase(cam)} dynamic {(Plugin.DynamicChaseEnabled.Value ? "on" : "off")}"
                            + $" active={_lastEnabled} applied={_lastApplied} weight={State.Weight:F2} side={State.Side:F3} lift={State.Lift:F3} lag={State.RollLag:F2}"
                            + $" rates p={_lastRates.x:F0} y={_lastRates.y:F0} r={_lastRates.z:F0}"
                            + (_probeSkipped > 0 ? $" (+{_probeSkipped} more)" : ""));
                        _nextProbeLog = Time.unscaledTime + 0.25f;
                        _probeSkipped = 0;
                    }
                    else _probeSkipped++;
                }
            }
            _prevStep = step; _hasStep = true;
        }
        else _hasStep = false;
        _prevDt = dt;
        if (cam.followingRB.interpolation != _loggedInterpolation)
        {
            _loggedInterpolation = cam.followingRB.interpolation;
            Plugin.Logger.LogInfo($"[Twitch] aircraft rigidbody interpolation: {_loggedInterpolation}, physics step {Time.fixedDeltaTime * 1000f:F1} ms");
        }
        _prevView = view; _prevPivot = pivot; _hasView = true;
    }
}
