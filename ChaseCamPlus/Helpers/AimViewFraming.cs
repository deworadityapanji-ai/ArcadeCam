using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class AimViewFraming
{
    // A centered Orbit rig would put the aircraft directly over the aim marker
    // when aligned with it. Keep at least 16% of frame height between the two,
    // while preserving a composition which already sits farther below the aim.
    internal static Vector3 LiftOffset(Vector3 offset, Vector3 forward, Vector3 up, float fov)
    {
        float depth = Math.Max(0f, -Vector3.Dot(offset, forward));
        float height = Vector3.Dot(offset, up);
        float halfFov = Math.Max(10f, Math.Min(150f, fov)) * (float)Math.PI / 360f;
        float minimum = depth * (float)Math.Tan(halfFov) * 0.32f;
        return offset + up * Math.Max(0f, minimum - height);
    }
}
