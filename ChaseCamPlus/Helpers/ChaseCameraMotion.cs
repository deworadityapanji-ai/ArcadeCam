using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChaseCameraMotion
{
    private static readonly CameraMotionState State = new CameraMotionState();
    internal static void Reset() => State.Reset();

    internal static void Apply(CameraStateManager cam, bool manualLook, bool tracking)
    {
        if (cam.followingRB == null) return;
        Transform aircraft = cam.followingUnit.transform;
        float pitchUpRate = -Vector3.Dot(cam.followingRB.angularVelocity, aircraft.right) * Mathf.Rad2Deg;
        State.Advance(cam.followingRB.velocity.magnitude, pitchUpRate,
            Plugin.SpeedMotionEnabled.Value, Plugin.SpeedMotionStrength.Value,
            Plugin.PitchFollowEnabled.Value && !manualLook && !tracking,
            Plugin.PitchFollowStrength.Value, Plugin.PitchFollowLimit.Value,
            Plugin.MotionResponse.Value, Time.deltaTime);
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 offset = cam.transform.position - pivot;
        cam.transform.position = pivot + offset * State.DistanceMultiplier;
        if (Mathf.Abs(State.PitchDegrees) > 0.001f)
        {
            // Positive pitch rate opens space above the nose, without lagging roll.
            cam.transform.position += aircraft.up * (offset.magnitude * Mathf.Tan(State.PitchDegrees * Mathf.Deg2Rad) * 0.2f);
            cam.transform.rotation = Quaternion.AngleAxis(-State.PitchDegrees, aircraft.right) * cam.transform.rotation;
        }
        CameraTerrainAvoidance.Apply(cam, pivot);
    }
}