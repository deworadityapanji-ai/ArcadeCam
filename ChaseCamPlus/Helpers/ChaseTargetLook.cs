using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChaseTargetLook
{
    private static readonly TargetLookBlend Blend = new TargetLookBlend();
    private static Quaternion _targetRotation = Quaternion.identity;
    private static Vector3 _targetOffset;
    private static Vector3 _baseLocalPosition;
    private static Quaternion _baseLocalRotation = Quaternion.identity;
    private static Transform _cameraTransform;
    private static bool _hasPoseOverride;
    private static bool _suppressUntilRelease;

    internal static bool IsTransitioning => Blend.Weight > 0f;

    internal static void Reset()
    {
        Blend.Reset();
        _targetRotation = Quaternion.identity;
        _targetOffset = Vector3.zero;
        _cameraTransform = null;
        _hasPoseOverride = false;
        _suppressUntilRelease = false;
    }

    internal static void CancelUntilReleased()
    {
        Reset();
        _suppressUntilRelease = true;
    }

    internal static void RestoreBasePose(CameraStateManager cam)
    {
        // Neither the target-facing position nor rotation may feed back into vanilla's
        // chase smoothing. Local coordinates also survive aircraft/origin movement.
        if (_hasPoseOverride && cam != null && _cameraTransform == cam.transform)
        {
            cam.transform.localPosition = _baseLocalPosition;
            cam.transform.localRotation = _baseLocalRotation;
        }
        _hasPoseOverride = false;
    }

    internal static bool IsActive() =>
        TryGetTargetPosition(SceneSingleton<CameraStateManager>.i, out _);

    internal static bool TryGetTargetPosition(CameraStateManager cam, out Vector3 position)
    {
        position = Vector3.zero;
        var player = GameManager.playerInput;
        if (player != null && !player.GetButton("Cycle Look At"))
            _suppressUntilRelease = false;

        if (_suppressUntilRelease || !Plugin.LookAtTargetInChase.Value
            || !ChaseInput.IsAllowed(cam) || cam.orbitState == null)
            return false;

        // Reuse the native binding and selected-target resolver.
        if (!cam.orbitState.LookingAtTarget(cam, out Unit target) || target == null)
            return false;
        if (GameManager.GetLocalHQ(out var localHq) && !localHq.IsTargetPositionAccurate(target, 100f))
            return false;
        var hq = cam.followingUnit.NetworkHQ;
        if (hq == null || !hq.TryGetKnownPosition(target, out var knownPosition))
            return false;

        position = knownPosition.ToLocalPosition();
        return true;
    }

    internal static void Apply(CameraStateManager cam, bool tracking, Vector3 targetPosition)
    {
        Vector3 pivot = cam.cameraPivot.position;
        Vector3 basePosition = cam.transform.position;
        Vector3 baseOffset = basePosition - pivot;
        Quaternion baseRotation = cam.transform.rotation;
        float smoothing = Mathf.Max(PlayerSettings.viewSmoothing, 0.01f);
        float t = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime / smoothing);

        if (tracking)
        {
            if ((targetPosition - pivot).sqrMagnitude < 0.0001f)
                tracking = false;
            else
            {
                if (Blend.Weight <= 0f)
                {
                    _targetRotation = baseRotation;
                    _targetOffset = baseOffset;
                }

                Vector3 wantedOffset = baseOffset;
                if (Plugin.MoveCameraForTargetLook.Value)
                {
                    float distance = Mathf.Max(baseOffset.magnitude, cam.followingUnit.maxRadius * 2f + 1f);
                    Vector3 up = CinematicCamera.Active ? Vector3.up : cam.followingUnit.transform.up;
                    wantedOffset = TargetCameraFraming.Offset(targetPosition - pivot, up, distance);
                }
                // Spherical movement avoids crossing through the aircraft on a rear target.
                _targetOffset = Vector3.Slerp(_targetOffset, wantedOffset, t);
            }
        }

        float weight = Blend.Advance(tracking, Time.deltaTime, Plugin.TargetReturnSeconds.Value);
        if (weight <= 0f)
            return;

        _baseLocalPosition = cam.transform.localPosition;
        _baseLocalRotation = cam.transform.localRotation;
        _cameraTransform = cam.transform;
        _hasPoseOverride = true;

        float blend = Mathf.SmoothStep(0f, 1f, weight);
        cam.transform.position = pivot + Vector3.Slerp(baseOffset, _targetOffset, blend);
        CameraTerrainAvoidance.Apply(cam, pivot);

        if (tracking)
        {
            Vector3 direction = targetPosition - cam.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Vector3 up = CinematicCamera.Active ? Vector3.up : cam.followingUnit.transform.up;
                up = Vector3.ProjectOnPlane(up, direction.normalized);
                if (up.sqrMagnitude < 0.0001f)
                    up = Vector3.ProjectOnPlane(cam.transform.right, direction.normalized);
                if (up.sqrMagnitude < 0.0001f)
                    up = Vector3.ProjectOnPlane(cam.transform.forward, direction.normalized);
                _targetRotation = Quaternion.Slerp(
                    _targetRotation, Quaternion.LookRotation(direction, up), t);
            }
        }
        cam.transform.rotation = Quaternion.Slerp(baseRotation, _targetRotation, blend);
    }
}