using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class CenteringToggle
{
    private static int _lastFrame = -1;
    internal static void Poll()
    {
        if (_lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (!ChaseInput.IsAllowed(cam) || Plugin.CenteringToggleActionId < 0) return;
        if (GameManager.playerInput.GetButtonDown(Plugin.CenteringToggleActionId))
            Plugin.CenteringEnabled.Value = !Plugin.CenteringEnabled.Value;
    }
}