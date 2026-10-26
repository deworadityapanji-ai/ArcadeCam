using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChaseFreeLook
{
    private static readonly FreeLookInputState State = new FreeLookInputState();

    private static bool _armOnFirstAllowedInput;

    internal static void Reset()
    {
        State.Reset(Time.frameCount);
        _armOnFirstAllowedInput = false;
    }

    internal static void EnterChase()
    {
        Reset();
        _armOnFirstAllowedInput = Plugin.FreeLookOnChaseEntry.Value && Plugin.ToggleFreeLook.Value;
    }

    internal static void Poll() => IsActive();

    internal static bool IsActive()
    {
        var player = GameManager.playerInput;
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        bool allowed = ChaseInput.IsAllowed(cam);
        if (_armOnFirstAllowedInput && allowed)
        {
            State.Reset(Time.frameCount, true, Plugin.ToggleFreeLook.Value);
            _armOnFirstAllowedInput = false;
        }

        // UI input is suspended, while the chosen toggle state survives until
        // flight resumes. Read the physical button even in menus to discard a
        // press held across closing. Entering/leaving chase still resets State.
        return State.Read(
            player != null && Plugin.FreeLookActionId >= 0 && player.GetButton(Plugin.FreeLookActionId),
            player != null && Plugin.FreeLookActionId >= 0 && player.GetButtonDown(Plugin.FreeLookActionId),
            Plugin.ToggleFreeLook.Value,
            allowed,
            Time.frameCount);
    }
}