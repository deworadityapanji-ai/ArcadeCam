using UnityEngine;

namespace ArcadeCam.Helpers;

// An upright, aircraft-centered rig. The normal chase state still owns input,
// HUD, FoV and the followed aircraft, so all ArcadeCam overlays compose with it.
internal static class OrbitCamera
{
    private static readonly OrbitCourse Course = new();
    private static Quaternion _rotation;
    private static bool _ready;

    internal static void Reset() { Course.Reset(); _ready = false; }

    internal static void ReadPose(CameraStateManager cam, float zoom,
        out Vector3 offset, out Quaternion rotation)
    {
        float dt = Time.unscaledDeltaTime;
        Vector3 heading = Course.Advance(cam.followingRB.velocity, cam.followingRB.transform.forward, dt);
        Quaternion desired = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(20f, 0f, 0f);
        _rotation = _ready
            ? Quaternion.Lerp(_rotation, desired, Mathf.Clamp01(5f * dt / Mathf.Max(PlayerSettings.viewSmoothing, 0.01f)))
            : desired;
        _ready = true;
        rotation = _rotation;
        offset = -(rotation * Vector3.forward) * OrbitCourse.Distance(cam.followingUnit.maxRadius, zoom);
    }
}
