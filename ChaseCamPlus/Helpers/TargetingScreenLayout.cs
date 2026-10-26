using System;

namespace ArcadeCam.Helpers;

internal readonly struct TargetingScreenRect
{
    internal readonly float Left, Top, Width, Height;
    internal TargetingScreenRect(float left, float top, float width, float height)
    { Left = left; Top = top; Width = width; Height = height; }
}

internal static class TargetingScreenLayout
{
    // Positions describe travel across the available space, keeping the entire panel
    // inside the viewport. Coordinates are pixels measured from its top-left corner.
    internal static TargetingScreenRect Calculate(float viewportWidth, float viewportHeight,
        float xPercent, float yPercent, float widthPercent, float aspect)
    {
        float screenWidth = Math.Max(1f, viewportWidth), screenHeight = Math.Max(1f, viewportHeight);
        aspect = float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f ? 1.8f : aspect;
        float margin = Math.Min(12f, Math.Min(screenWidth, screenHeight) * 0.02f);
        float availableWidth = Math.Max(1f, screenWidth - 2f * margin);
        float availableHeight = Math.Max(1f, screenHeight - 2f * margin);
        float width = Math.Min(availableWidth, screenWidth * Clamp(widthPercent, 10f, 50f) / 100f);
        width = Math.Min(width, availableHeight * aspect);
        float height = width / aspect;
        return new TargetingScreenRect(
            margin + (availableWidth - width) * Clamp(xPercent, 0f, 100f) / 100f,
            margin + (availableHeight - height) * Clamp(yPercent, 0f, 100f) / 100f,
            width, height);
    }
    internal static TargetingScreenRect FitImage(float frameWidth, float frameHeight, float padding, float aspect)
    {
        aspect = float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f ? 1.8f : aspect;
        float usableWidth = Math.Max(0f, frameWidth - 2f * Math.Max(padding, 0f));
        float usableHeight = Math.Max(0f, frameHeight - 2f * Math.Max(padding, 0f));
        float width = Math.Min(usableWidth, usableHeight * aspect);
        float height = width / aspect;
        return new TargetingScreenRect((frameWidth - width) * 0.5f, (frameHeight - height) * 0.5f, width, height);
    }
    private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
}

internal readonly struct TargetingScreenState
{
    internal readonly bool Enabled, ChaseView, HudVisible, LocalAircraft, HasTarget, CameraRunning, FeedReady, LandingMode, InputAllowed;
    internal TargetingScreenState(bool enabled, bool chaseView, bool hudVisible, bool localAircraft,
        bool hasTarget, bool cameraRunning, bool feedReady, bool landingMode, bool inputAllowed)
    {
        Enabled = enabled; ChaseView = chaseView; HudVisible = hudVisible; LocalAircraft = localAircraft;
        HasTarget = hasTarget; CameraRunning = cameraRunning; FeedReady = feedReady;
        LandingMode = landingMode; InputAllowed = inputAllowed;
    }
    internal bool Visible => Enabled && ChaseView && HudVisible && LocalAircraft && HasTarget
                             && CameraRunning && FeedReady && !LandingMode && InputAllowed;
}