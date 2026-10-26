using System;

namespace ArcadeCam.Helpers;

internal sealed class ViewReturnBlend
{
    private float _elapsed, _duration;
    internal bool Active { get; private set; }
    internal void Begin(float seconds) { _elapsed = 0f; _duration = Math.Max(0f, seconds); Active = _duration > 0f; }
    internal float Advance(float dt)
    {
        if (!Active) return 1f;
        _elapsed = Math.Min(_duration, _elapsed + Math.Max(dt, 0f));
        float t = _elapsed / _duration;
        if (t >= 1f) Active = false;
        return t * t * (3f - 2f * t);
    }
    internal void Reset() { Active = false; _elapsed = _duration = 0f; }
}