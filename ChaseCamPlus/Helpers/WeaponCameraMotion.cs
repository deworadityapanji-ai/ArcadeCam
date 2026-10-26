using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal sealed class WeaponCameraMotion
{
    private bool _initialized;
    private Vector3 _lastForward;
    private float _side = -1f, _sideTarget = -1f;
    internal float Side { get; private set; }
    internal float Distance { get; private set; }
    internal float Height { get; private set; }

    internal void Reset() { _initialized = false; _side = _sideTarget = -1f; }

    internal void Advance(Vector3 forward, Vector3 right, float speed, float radius, float scale, bool cinematic, float dt)
    {
        forward.Normalize();
        float turn = _initialized && dt > 0.0001f && dt <= 0.25f
            ? Vector3.Dot(forward - _lastForward, right) / dt : 0f;
        _lastForward = forward; _initialized = true;
        if (turn > 0.12f) _sideTarget = -1f;
        else if (turn < -0.12f) _sideTarget = 1f;
        float blend = dt <= 0f ? 0f : 1f - (float)Math.Exp(-dt / 0.4f);
        _side += (_sideTarget - _side) * blend;
        Distance = Math.Max(8f, Math.Min(40f, Math.Max(0f, radius) * 4f + 8f))
            * Math.Max(0.5f, Math.Min(3f, scale))
            * (1f + (cinematic ? Math.Max(0f, Math.Min(1f, speed / 1000f)) * 0.2f : 0f));
        Side = cinematic ? _side * 0.5f : 0f;
        Height = cinematic ? 0.22f : 0.12f;
    }

    internal static Vector3 Right(Vector3 forward, Vector3 previous)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f)
            right = (previous - forward * Vector3.Dot(previous, forward)).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(Vector3.forward, forward).normalized;
        right.Normalize();
        if (Vector3.Dot(right, previous) < 0f) right = -right;
        return right;
    }
}
