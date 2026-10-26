using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChaseViewReturn
{
    private static readonly ViewReturnBlend Blend = new ViewReturnBlend();
    private static CameraStateManager _camera;
    private static Unit _unit;
    private static Vector3 _lastOffset, _startOffset;
    private static Quaternion _lastRotation, _startRotation;
    private static bool _remembered;
    internal static bool IsActive => Blend.Active;

    internal static void Reset()
    {
        Blend.Reset(); _camera = null; _unit = null; _remembered = false;
    }

    internal static void Begin(CameraStateManager cam)
    {
        if (cam == null || cam.cameraPivot == null) return;
        // Center can be pressed inside vanilla UpdateState after our previous visual
        // pose was restored. Use the last rendered pose, not that temporary native pose.
        bool valid = _remembered && _camera == cam && _unit == cam.followingUnit;
        _startOffset = valid ? _lastOffset : cam.transform.position - cam.cameraPivot.position;
        _startRotation = valid ? _lastRotation : cam.transform.rotation;
        Blend.Begin(Plugin.TargetReturnSeconds.Value);
    }

    internal static void Apply(CameraStateManager cam)
    {
        if (Blend.Active)
        {
            float t = Blend.Advance(Time.deltaTime);
            Vector3 pivot = cam.cameraPivot.position;
            cam.transform.position = pivot + Vector3.Slerp(_startOffset, cam.transform.position - pivot, t);
            cam.transform.rotation = Quaternion.Slerp(_startRotation, cam.transform.rotation, t);
            CameraTerrainAvoidance.Apply(cam, pivot);
        }
        _lastOffset = cam.transform.position - cam.cameraPivot.position;
        _lastRotation = cam.transform.rotation;
        _camera = cam;
        _unit = cam.followingUnit;
        _remembered = true;
    }
}