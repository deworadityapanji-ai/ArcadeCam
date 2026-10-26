using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class CameraGeometryMath
{
    internal static float MinimumZoomAdjustment(float fraction) =>
        Math.Max(0.1f, Math.Min(1f, fraction)) - 1f;

    internal static Vector3 AdjustOffset(Vector3 offset, Vector3 up, float scale, float height) =>
        offset * Math.Max(0.1f, scale) + up * height;
}