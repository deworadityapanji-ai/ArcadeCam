using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Direction smoothing and horizon leveling are independent. Aircraft bank is
// deliberately not an input: changing roll cannot steer the held world heading.
internal sealed class MouseAimFrame
{
    private bool _ready, _nearPole;
    internal Vector3 Forward { get; private set; } = Vector3.forward;
    internal Vector3 Up { get; private set; } = Vector3.up;
    internal Vector3 ReferenceUp => _nearPole ? Up : Vector3.up;

    internal void Reset()
    {
        _ready = _nearPole = false;
        Forward = Vector3.forward; Up = Vector3.up;
    }

    internal void Advance(Vector3 aim, Vector3 entryForward, float response, float dt)
    {
        if (!Finite(aim) || aim.sqrMagnitude < 0.000001f) return;
        aim.Normalize();
        if (!_ready)
        {
            Forward = Finite(entryForward) && entryForward.sqrMagnitude > 0.000001f ? entryForward.normalized : aim;
            Up = Perpendicular(Vector3.up, Forward);
            _ready = true;
        }
        float weight = Finite(dt) && Finite(response)
            ? 1f - (float)Math.Exp(-Math.Max(0.1f, response) * Math.Max(0f, dt)) : 0f;
        Forward = Turn(Forward, aim, Up, weight);
        Vector3 transportedUp = Perpendicular(Up, Forward);
        float vertical = Math.Abs(Forward.y);
        if (vertical > 0.99f) _nearPole = true;
        else if (vertical < 0.95f) _nearPole = false;
        if (_nearPole) { Up = transportedUp; return; }
        Vector3 level = Perpendicular(Vector3.up, Forward);
        float angle = (float)Math.Atan2(Vector3.Dot(Forward, Vector3.Cross(transportedUp, level)),
            Clamp(Vector3.Dot(transportedUp, level)));
        Up = Perpendicular(Rotate(transportedUp, Forward, angle * weight), Forward);
    }

    private static Vector3 Turn(Vector3 from, Vector3 to, Vector3 up, float weight)
    {
        if (weight <= 0f) return from;
        if (weight >= 1f) return to;
        float dot = Clamp(Vector3.Dot(from, to));
        if (dot > 0.9999f) return (from + (to - from) * weight).normalized;
        Vector3 axis = Vector3.Cross(from, to);
        if (axis.sqrMagnitude < 0.000001f) axis = Perpendicular(up, from);
        else axis.Normalize();
        return Rotate(from, axis, (float)Math.Acos(dot) * weight).normalized;
    }

    private static Vector3 Perpendicular(Vector3 reference, Vector3 direction)
    {
        Vector3 value = reference - direction * Vector3.Dot(reference, direction);
        if (value.sqrMagnitude < 0.000001f)
        {
            Vector3 fallback = Math.Abs(direction.z) < 0.9f ? Vector3.forward : Vector3.right;
            value = fallback - direction * Vector3.Dot(fallback, direction);
        }
        return value.normalized;
    }

    private static Vector3 Rotate(Vector3 value, Vector3 axis, float angle)
    {
        float cosine = (float)Math.Cos(angle), sine = (float)Math.Sin(angle);
        return value * cosine + Vector3.Cross(axis, value) * sine
            + axis * Vector3.Dot(axis, value) * (1f - cosine);
    }

    private static float Clamp(float value) => Math.Max(-1f, Math.Min(1f, value));
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}
