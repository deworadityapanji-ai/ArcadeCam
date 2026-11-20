using System;

namespace ArcadeCam.Helpers;

/// <summary>
/// Spring-damped "dynamic" offsets layered on the normal (aircraft-relative) chase camera.
/// Pure math (no Unity types) so it can be unit tested.
///
/// Inputs are the aircraft's local rotation rates in degrees per second, using Unity's axes:
/// pitch is +X (nose down), yaw is +Y (nose right) and roll is +Z (roll left).
/// Outputs are fractions of the boom length (screen right/up) plus a roll-lag angle.
///
/// The camera stays welded to the aircraft like vanilla: nothing here lags the aim, so landing
/// and gunnery stay readable. It only leans the camera into what the aircraft is doing.
/// </summary>
internal sealed class DynamicCameraState
{
    // Rates that reach roughly 76% (tanh(1)) of the maximum response.
    internal const float RollFullRate = 180f, PitchFullRate = 25f, YawFullRate = 20f;
    // Maximum travel at strength 1, as a fraction of the boom length (or degrees for roll lag).
    internal const float RollSideMax = 0.10f, PitchLiftMax = 0.14f, YawSideMax = 0.05f;
    internal const float TotalMax = 0.3f, RollLagMaxDegrees = 8f;
    /// <summary>
    /// Light smoothing of the raw rotation rates, in seconds. The rigidbody's angular velocity is a
    /// per-physics-step value that jitters during a sustained roll (fly-by-wire corrections, and
    /// frames with zero or two physics steps); fed straight in, that shows up as a twitch.
    /// </summary>
    internal const float InputSmoothing = 0.1f;
    private float _pitchRate, _yawRate, _rollRate;
    private bool _hasRates;

    private float _sideVelocity, _liftVelocity, _rollVelocity, _weightVelocity;
    internal float Side { get; private set; }
    internal float Lift { get; private set; }
    internal float RollLag { get; private set; }
    internal float Weight { get; private set; }

    internal void Reset()
    {
        Side = Lift = RollLag = Weight = 0f;
        _sideVelocity = _liftVelocity = _rollVelocity = _weightVelocity = 0f;
        _pitchRate = _yawRate = _rollRate = 0f;
        _hasRates = false;
    }

    /// <param name="enabled">False eases everything back to the plain chase pose.</param>
    /// <param name="deadzone">Rates below this (deg/s) are ignored, so cruise and small corrections stay still.</param>
    /// <param name="response">Spring smoothing time in seconds.</param>
    internal void Advance(float pitchRate, float yawRate, float rollRate, bool enabled,
        float strength, float rollLag, float deadzone, float response, float dt)
    {
        if (dt <= 0f) return;
        // Frame-rate independent low-pass on the inputs (see InputSmoothing).
        float k = _hasRates ? 1f - (float)Math.Exp(-dt / InputSmoothing) : 1f;
        _pitchRate += (pitchRate - _pitchRate) * k;
        _yawRate += (yawRate - _yawRate) * k;
        _rollRate += (rollRate - _rollRate) * k;
        _hasRates = true;
        pitchRate = _pitchRate; yawRate = _yawRate; rollRate = _rollRate;
        strength = Clamp(strength, 0f, 3f);
        // Counter-roll: rolling right (negative Z) slides the camera left, so the aircraft
        // swings toward the outside of the roll instead of spinning rigidly in place.
        float side = Shape(rollRate, deadzone, RollFullRate) * RollSideMax
                     // Yaw: the camera trails the turn, showing a little of the aircraft's side.
                     + Shape(yawRate, deadzone, YawFullRate) * YawSideMax;
        // Pulling up (negative X) RAISES the camera, so the aircraft drops toward the bottom of
        // the frame and the space it is turning into opens up above it.
        float lift = -Shape(pitchRate, deadzone, PitchFullRate) * PitchLiftMax;
        side *= strength; lift *= strength;
        float length = (float)Math.Sqrt(side * side + lift * lift);
        if (length > TotalMax)
        {
            // Keeps the aircraft comfortably in frame however hard the inputs are.
            side *= TotalMax / length; lift *= TotalMax / length;
        }
        // Roll lag: the camera's roll trails the aircraft slightly (positive = rolls left).
        float lag = -Shape(rollRate, deadzone, RollFullRate) * RollLagMaxDegrees * Clamp(rollLag, 0f, 1f);

        Side = CameraMotionState.Spring(Side, side, ref _sideVelocity, response, dt);
        Lift = CameraMotionState.Spring(Lift, lift, ref _liftVelocity, response, dt);
        RollLag = CameraMotionState.Spring(RollLag, lag, ref _rollVelocity, response, dt);
        // Looking around, target tracking etc. fade the effect out instead of snapping.
        Weight = Clamp(CameraMotionState.Spring(Weight, enabled ? 1f : 0f, ref _weightVelocity, 0.3f, dt), 0f, 1f);
    }

    /// <summary>Signed, deadzoned and softly saturated response in -1..1.</summary>
    internal static float Shape(float rate, float deadzone, float fullRate)
    {
        float excess = Math.Abs(rate) - Math.Max(deadzone, 0f);
        if (excess <= 0f) return 0f;
        return Math.Sign(rate) * (float)Math.Tanh(excess / Math.Max(fullRate, 0.01f));
    }

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}
