using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChaseCameraGeometry
{
    private static Transform _transform;
    private static Transform _parent;
    private static Vector3 _localPosition;
    private static Quaternion _localRotation;
    private static bool _captured;

    internal static float MinimumZoomAdjustment() =>
        CameraGeometryMath.MinimumZoomAdjustment(Plugin.MinimumZoomFraction.Value);

    internal static void Reset() { _captured = false; _transform = null; _parent = null; }

    internal static void Capture(CameraStateManager cam)
    {
        _transform = cam.transform;
        _parent = _transform.parent;
        _localPosition = _transform.localPosition;
        _localRotation = _transform.localRotation;
        _captured = true;
    }

    internal static void Restore(CameraStateManager cam)
    {
        if (_captured && cam != null && _transform == cam.transform && _parent == _transform.parent)
        {
            _transform.localPosition = _localPosition;
            _transform.localRotation = _localRotation;
        }
        _captured = false;
    }

    internal static void Apply(CameraStateManager cam)
    {
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 up = CinematicCamera.Active ? Vector3.up : cam.followingUnit.transform.up;
        cam.transform.position = pivot + CameraGeometryMath.AdjustOffset(
            cam.transform.position - pivot, up,
            Plugin.CameraDistanceScale.Value, Plugin.CameraHeightOffset.Value);
        CameraTerrainAvoidance.Apply(cam, pivot);
    }
}