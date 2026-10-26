using System;

namespace ArcadeCam.Helpers;

// Only boom length and pitch anticipation are damped. Roll remains immediate.
internal sealed class CameraMotionState
{
    private bool _initialized;
    private float _previousSpeed, _acceleration;
    private float _distanceVelocity, _pitchVelocity;
    internal float DistanceMultiplier { get; private set; } = 1f;
    internal float PitchDegrees { get; private set; }

    internal void Reset()
    {
        _initialized = false;
        _previousSpeed = _acceleration = _distanceVelocity = _pitchVelocity = 0f;
        DistanceMultiplier = 1f;
        PitchDegrees = 0f;
    }

    internal void Advance(float speed, float pitchUpRate, bool speedEnabled, float speedStrength,
        bool pitchEnabled, float pitchStrength, float pitchLimit, float response, float dt)
    {
        if (dt <= 0f) return;
        speed = Math.Max(0f, speed);
        float sample = _initialized && dt <= 0.25f ? Clamp((speed - _previousSpeed) / dt, -60f, 60f) : 0f;
        // A filtered acceleration avoids pumping the boom with physics sample noise.
        _acceleration = !_initialized || dt > 0.25f ? 0f
            : _acceleration + (sample - _acceleration) * (1f - (float)Math.Exp(-dt / 0.12f));
        _previousSpeed = speed;
        _initialized = true;
        // More sensitivity to ordinary cruise/acceleration, with the original .82..1.30
        // travel envelope. Braking retains its gentler 40 m/s² scale.
        float accelerationScale = _acceleration >= 0f ? 10f : 40f;
        float distance = speedEnabled ? 1f + Clamp(speedStrength, 0f, 1f)
            * (0.12f * Clamp(speed / 250f, 0f, 1f) + 0.18f * Clamp(_acceleration / accelerationScale, -1f, 1f)) : 1f;
        float limit = Clamp(pitchLimit, 0f, 20f);
        // Increase response to modest pitch rates, not the user's configured limit.
        float pitch = pitchEnabled ? Clamp(pitchUpRate * Math.Max(pitchStrength, 0f) * 3f, -limit, limit) : 0f;
        float boom = Spring(DistanceMultiplier, distance, ref _distanceVelocity, response, dt);
        float aim = Spring(PitchDegrees, pitch, ref _pitchVelocity, response, dt);
        DistanceMultiplier = Clamp(boom, 0.82f, 1.30f);
        PitchDegrees = Clamp(aim, -limit, limit);
        if (boom != DistanceMultiplier) _distanceVelocity = 0f;
        if (aim != PitchDegrees) _pitchVelocity = 0f;
    }

    // Exact critically damped spring for a constant target over this frame. Velocity
    // remains continuous when input changes, unlike directly lerping to a rate sample.
    private static float Spring(float current, float target, ref float velocity, float response, float dt)
    {
        if (response <= 0f) { velocity = 0f; return target; }
        float omega = 2f / Math.Max(response, 0.01f);
        float offset = current - target;
        float momentum = velocity + omega * offset;
        float decay = (float)Math.Exp(-omega * dt);
        velocity = (velocity - omega * momentum * dt) * decay;
        return target + (offset + momentum * dt) * decay;
    }

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}