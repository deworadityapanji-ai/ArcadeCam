using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class FreeLookAngles
{
    internal const float PoleLimit = 89.5f;

    // Yaw wraps at the rear seam; pitch must never wrap through a pole.
    internal static float Wrap(float degrees)
    {
        float angle = (degrees + 180f) % 360f;
        if (angle < 0f) angle += 360f;
        return angle - 180f;
    }

    internal static float Step(float current, float desired, float blend) =>
        Wrap(current + Wrap(desired - current) * Math.Max(0f, Math.Min(1f, blend)));

    internal static float Elevation(Vector3 forward, Vector3 up) =>
        (float)(Math.Asin(Math.Max(-1f, Math.Min(1f, Vector3.Dot(forward.normalized, up.normalized)))) * 180.0 / Math.PI);

    internal static float ClampTilt(float tilt, float baseElevation) =>
        Math.Max(baseElevation - PoleLimit, Math.Min(baseElevation + PoleLimit, tilt));

    internal static float StepTilt(float current, float desired, float blend, float baseElevation) =>
        ClampTilt(current + (ClampTilt(desired, baseElevation) - current) * Math.Max(0f, Math.Min(1f, blend)), baseElevation);

    // Express a world viewing direction in a new orbit frame. Reusing the old
    // angle numbers after a horizon switch points somewhere else when banked.
    internal static Vector2 Rebase(Vector3 direction, Vector3 baseForward, Vector3 up)
    {
        Frame(baseForward, up, out Vector3 right, out Vector3 heading);
        direction.Normalize();
        float pan = (float)(Math.Atan2(Vector3.Dot(direction, right), Vector3.Dot(direction, heading)) * 180.0 / Math.PI);
        float tilt = Elevation(baseForward, up) - Elevation(direction, up);
        return new Vector2(Wrap(pan), ClampTilt(tilt, Elevation(baseForward, up)));
    }

    internal static Vector3 Direction(float pan, float tilt, Vector3 baseForward, Vector3 up)
    {
        up.Normalize();
        Frame(baseForward, up, out Vector3 right, out Vector3 heading);
        double yaw = pan * Math.PI / 180.0;
        double elevation = (Elevation(baseForward, up) - tilt) * Math.PI / 180.0;
        Vector3 horizontal = heading * (float)Math.Cos(yaw) + right * (float)Math.Sin(yaw);
        return (horizontal * (float)Math.Cos(elevation) + up * (float)Math.Sin(elevation)).normalized;
    }

    private static void Frame(Vector3 forward, Vector3 up, out Vector3 right, out Vector3 heading)
    {
        up.Normalize();
        right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(up, Math.Abs(up.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
        heading = Vector3.Cross(right, up).normalized;
    }
    internal static Vector3 YawAxis(Vector3 forward, Vector3 cameraUp, Vector3 referenceUp)
    {
        // A world-up yaw axis becomes a roll axis when a level-horizon rig flies
        // vertically. Ease into screen-up near that pole instead of locking pan.
        float alignment = Math.Abs(Vector3.Dot(forward.normalized, referenceUp.normalized));
        float t = Math.Max(0f, Math.Min(1f, (alignment - 0.8f) / 0.2f));
        t = t * t * (3f - 2f * t);
        Vector3 up = referenceUp * (1f - t) + cameraUp * t;
        return up.sqrMagnitude > 0.0001f ? up.normalized : cameraUp;
    }
}
