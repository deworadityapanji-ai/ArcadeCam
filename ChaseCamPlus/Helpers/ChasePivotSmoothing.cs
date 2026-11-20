using UnityEngine;

namespace ArcadeCam.Helpers;

/// <summary>
/// Replaces the chase camera's rotation smoothing with a frame-rate independent version.
///
/// The game turns the camera pivot toward the aircraft's attitude by <c>5 × frameTime / smoothing</c>
/// of the remaining angle each frame. During a steady roll that leaves a lag of roughly
/// <c>rollRate × (smoothing/5 − frameTime)</c>, which depends on the frame time: when frames go
/// uneven (20 ms, then 38 ms) the lag changes abruptly and the aircraft visibly jumps a degree or
/// three on screen. Measured in the log as "[Twitch] … base camera roll=…".
///
/// This keeps the same time constant (smoothing/5) but solves it exactly for each frame's length,
/// assuming the aircraft turned at a steady rate during the frame. A steady roll then has the
/// same lag at any frame rate, so uneven frames no longer show.
/// </summary>
internal static class ChasePivotSmoothing
{
    private static bool _has;
    private static Unit _unit;
    private static Quaternion _prevTarget, _prevPivot;

    internal static void Reset() { _has = false; _unit = null; }

    internal static void Apply(CameraStateManager cam, Vector3 targetVector, ref Quaternion rotationPrev)
    {
        if (!Plugin.FrameRateIndependentChase.Value || cam.cameraPivot == null || cam.followingUnit == null) { Reset(); return; }
        Transform aircraft = cam.followingUnit.transform;
        Vector3 forward = aircraft.right * targetVector.x + aircraft.up * targetVector.y + aircraft.forward * targetVector.z;
        if (forward.sqrMagnitude < 0.000001f) { Reset(); return; }
        Quaternion target = Quaternion.LookRotation(forward, aircraft.up);
        float tau = Mathf.Max(PlayerSettings.viewSmoothing, 0f) / 5f;
        float dt = Time.unscaledDeltaTime;

        Quaternion pivot;
        if (!_has || _unit != cam.followingUnit)
            pivot = cam.cameraPivot.rotation;               // start from where the game put it
        else if (tau < 0.002f || dt <= 0f)
            pivot = tau < 0.002f ? target : _prevPivot;     // no smoothing / paused
        else
        {
            Vector3 turned = AngleVector(target * Quaternion.Inverse(_prevTarget));   // the aircraft's turn this frame
            if (turned.magnitude > 60f) pivot = target;                               // respawn / teleport: snap
            else
            {
                // Lag e follows de/dt = rate − e/τ with a steady rate over the frame; exact solution.
                Vector3 lag = AngleVector(_prevTarget * Quaternion.Inverse(_prevPivot));
                float decay = Mathf.Exp(-dt / tau);
                Vector3 rate = turned / dt;
                Vector3 next = rate * tau * (1f - decay) + lag * decay;
                float angle = next.magnitude;
                pivot = angle > 0.0001f ? Quaternion.Inverse(Quaternion.AngleAxis(angle, next / angle)) * target : target;
            }
        }
        cam.cameraPivot.rotation = pivot;
        rotationPrev = pivot;   // the game's own smoothing state continues from ours
        _prevTarget = target; _prevPivot = pivot; _unit = cam.followingUnit; _has = true;
    }

    // Rotation as an axis scaled by its angle in degrees (shortest way round).
    private static Vector3 AngleVector(Quaternion q)
    {
        q.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Abs(angle) < 0.0001f) return Vector3.zero;
        return axis.normalized * angle;
    }
}
