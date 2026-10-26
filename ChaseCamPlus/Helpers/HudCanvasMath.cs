using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class HudCanvasMath
{
    // Screen-space UI is defined by its pixel rectangle and local rectangle, not
    // the canvas's temporary world translation during a floating-origin shift.
    internal static bool TryScreenToLocal(Vector2 point, Rect pixels, Rect local, out Vector3 result)
    {
        result = Vector3.zero;
        if (!Finite(point.x) || !Finite(point.y) || !Finite(pixels.x) || !Finite(pixels.y)
            || !Finite(pixels.width) || !Finite(pixels.height) || pixels.width <= 0f || pixels.height <= 0f
            || !Finite(local.x) || !Finite(local.y) || !Finite(local.width) || !Finite(local.height)
            || local.width <= 0f || local.height <= 0f) return false;
        result = new Vector3(local.xMin + (point.x - pixels.xMin) / pixels.width * local.width,
            local.yMin + (point.y - pixels.yMin) / pixels.height * local.height, 0f);
        return Finite(result.x) && Finite(result.y);
    }

    internal static Vector3 RelativeOffset(Vector3 point, Vector3 referenceScale, Vector3 canvasScale) =>
        new Vector3(point.x * Ratio(referenceScale.x, canvasScale.x),
            point.y * Ratio(referenceScale.y, canvasScale.y), point.z * Ratio(referenceScale.z, canvasScale.z));

    internal static bool HasPositionDrift(Vector3 expected, Vector3 actual) =>
        !Finite(actual.x) || !Finite(actual.y) || !Finite(actual.z)
        || (expected - actual).sqrMagnitude > 0.0001f;

    private static float Ratio(float reference, float canvas) =>
        Math.Abs(canvas) > 0.000001f ? reference / canvas : 0f;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
