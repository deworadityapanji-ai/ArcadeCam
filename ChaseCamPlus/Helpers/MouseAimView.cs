using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class MouseAimView
{
    private static readonly MouseAimFrame Frame = new();
    internal static Vector3 ReferenceUp => Frame.ReferenceUp;
    internal static void Reset() => Frame.Reset();

    internal static void Apply(CameraStateManager cam)
    {
        if (!WtMouseAimCompatibility.TryAim(cam, out Vector3 aim, out float response)) { Reset(); return; }
        aim.Normalize();
        Quaternion basis = cam.transform.rotation;
        Vector3 offset = cam.transform.position - cam.cameraPivot.position;
        Frame.Advance(aim, cam.transform.forward, response, Time.unscaledDeltaTime);
        Quaternion rotation = Quaternion.LookRotation(Frame.Forward, Frame.Up);
        // Rotate the current ArcadeCam composition toward the aim, preserving
        // its relative framing/zoom/motion and using no cached world position.
        Vector3 composed = rotation * Quaternion.Inverse(basis) * offset;
        composed = AimViewFraming.LiftOffset(composed, Frame.Forward, Frame.Up,
            cam.mainCamera != null ? cam.mainCamera.fieldOfView : 60f);
        cam.transform.SetPositionAndRotation(cam.cameraPivot.position + composed, rotation);
        CameraTerrainAvoidance.Apply(cam, cam.cameraPivot.position);
    }
}
