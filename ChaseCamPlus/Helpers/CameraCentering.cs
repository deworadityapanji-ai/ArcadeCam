using System;

namespace ArcadeCam.Helpers;

internal static class CameraCentering
{
    internal static float Decay(float angle, float force, float deltaTime)
    {
        float strength = Math.Max(0f, Math.Min(1f, force));
        return angle * (float)Math.Exp(-8f * strength * strength * Math.Max(deltaTime, 0f));
    }
}