using System;

namespace ArcadeCam.Helpers;

/// <summary>Time-based handover between manual chase view and target look.</summary>
internal sealed class TargetLookBlend
{
    private const float TransitionSeconds = 0.25f;

    internal float Weight { get; private set; }

    internal float Advance(bool tracking, float deltaTime, float releaseSeconds = TransitionSeconds)
    {
        float duration = tracking ? TransitionSeconds : Math.Max(releaseSeconds, 0f);
        float step = duration <= 0f ? 1f : Math.Max(deltaTime, 0f) / duration;
        Weight = tracking ? Math.Min(1f, Weight + step) : Math.Max(0f, Weight - step);
        return Weight;
    }

    internal void Reset() => Weight = 0f;
}