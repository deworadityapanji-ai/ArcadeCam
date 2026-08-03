namespace ChaseCamPlus.Helpers;

/// <summary>
/// One key that swaps between the cockpit and the chase camera.
///
/// From any *other* camera — orbit, TV, free, relative — it returns to the cockpit rather than doing
/// nothing. A key you press mid-fight should never be inert, and "put me back at the controls" is
/// the most useful single meaning it can have.
/// </summary>
public static class CameraToggle
{
    public static void Poll()
    {
        if (!Plugin.Enabled.Value)
            return;

        if (Plugin.ToggleActionId < 0)
            return;

        var player = GameManager.playerInput;
        if (player == null || !player.GetButtonDown(Plugin.ToggleActionId))
            return;

        Toggle();
    }

    private static void Toggle()
    {
        // The game's own "is the player actually flying" flag, checked by both the cockpit and chase
        // states before they read any input. Deferring to it means menus, the map, the radial menu
        // and the pause screen suppress this key without us enumerating them.
        if (!GameManager.flightControlsEnabled)
            return;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null)
            return;

        // Hold-L camera picker is up: the player is already choosing a view deliberately, and the
        // chase state suppresses its own numpad shortcuts under the same condition.
        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        if (picker != null && picker.isOpen)
            return;

        if (cam.currentState == cam.cockpitState)
        {
            // CameraChaseState.EnterState diverts to the orbit camera when it is not following an
            // aircraft, so without this guard the key would silently land somewhere unasked for.
            if (cam.followingUnit is Aircraft)
                cam.SwitchState(cam.chaseState);

            return;
        }

        // CameraCockpitState.EnterState reads followingUnit as an Aircraft and immediately takes
        // pilots[0], so anything else throws; and it bails straight back out to the free camera if
        // that pilot is dead. Check both rather than switching into a state that cannot hold.
        if (cam.followingUnit is Aircraft aircraft
            && aircraft.pilots != null
            && aircraft.pilots.Length > 0
            && aircraft.pilots[0] != null
            && !aircraft.pilots[0].dead)
        {
            cam.SwitchState(cam.cockpitState);
        }
    }
}
