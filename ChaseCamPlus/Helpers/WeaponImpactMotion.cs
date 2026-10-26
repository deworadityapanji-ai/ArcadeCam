using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Damage near the observed blast is evidence of a hit; a guidance target alone is not.
internal sealed class WeaponImpactEvidence
{
    private float _damageTime = float.NegativeInfinity, _detonationTime;
    private bool _detonated, _nearby;
    internal bool Confirmed { get; private set; }

    internal void Reset()
    {
        _damageTime = float.NegativeInfinity;
        _detonated = _nearby = Confirmed = false;
    }

    internal void Damage(float time, float amount)
    {
        if (!(amount > 0f)) return;
        _damageTime = time;
        Confirm();
    }

    internal void Detonate(float time, bool nearby)
    {
        if (_detonated) return;
        _detonated = true; _detonationTime = time; _nearby = nearby;
        Confirm();
    }

    private void Confirm()
    {
        float delay = _damageTime - _detonationTime;
        if (_detonated && _nearby && delay >= -0.12f && delay <= 0.45f) Confirmed = true;
    }
}

internal sealed class WeaponImpactMotion
{
    private Vector3 _offset, _entry;

    internal void Begin(Vector3 offset)
    {
        _offset = _entry = offset.sqrMagnitude > 1f ? offset : new Vector3(-8f, 4f, -12f);
    }

    // Carry the subject's entire translation. Smooth only the camera boom so fast
    // wreckage cannot outrun the view, and do not inherit the wreckage's tumbling roll.
    internal Vector3 Advance(Vector3 point, float radius, float elapsed, float dt)
    {
        float angle = Math.Max(0f, elapsed) * 8f * (float)Math.PI / 180f;
        float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);
        Vector3 direction = new Vector3(_entry.x * cos + _entry.z * sin,
            Math.Max(_entry.y, _entry.magnitude * 0.2f), _entry.z * cos - _entry.x * sin).normalized;
        float distance = Math.Max(12f, Math.Min(200f, Math.Max(0f, radius) * 3f + 6f));
        distance = Math.Max(distance, Math.Min(200f, _entry.magnitude));
        float blend = dt <= 0f ? 0f : 1f - (float)Math.Exp(-dt / 0.25f);
        _offset = Vector3.Lerp(_offset, direction * distance, blend);
        float clearance = Math.Max(4f, Math.Min(150f, radius * 1.35f));
        if (_offset.sqrMagnitude < clearance * clearance) _offset = direction * clearance;
        return point + _offset;
    }

    // Absolute-time angular shake, applied only to the rendered camera. No random
    // state, accumulated rotation, flight-input motion or frame-rate dependency.
    internal static Vector3 Shake(float elapsed, float strength)
    {
        if (elapsed <= 0f || elapsed >= 0.4f || !(strength > 0f)) return Vector3.zero;
        float fade = 1f - elapsed / 0.4f;
        float amount = Math.Min(1f, strength) * fade * fade;
        return new Vector3((float)Math.Sin(elapsed * 145f) * 2.4f,
            (float)Math.Sin(elapsed * 193f) * 1.6f,
            (float)Math.Sin(elapsed * 121f) * 0.7f) * amount;
    }
}
