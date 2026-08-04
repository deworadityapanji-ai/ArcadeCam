namespace ChaseCamPlus.Helpers;

/// <summary>
/// Hold a key to drop into the cockpit, release it to go back to the chase camera you came from.
///
/// Third person is the better view for flying and the worse one for shooting — the cinematic camera
/// especially, which stops pointing where the aircraft does at all. Rather than make either view
/// compromise, this lets the player borrow the cockpit for the length of a gun pass and hands the
/// camera straight back.
///
/// Nothing needs saving and restoring. The cinematic camera's on/off flag is independent of which
/// camera state is running — <see cref="CinematicCamera.Reset"/> clears the smoothing but never the
/// flag — so returning to chase returns to exactly the chase you had, cinematic or plain, without
/// this having to remember which. Coming back it eases in over the usual transition instead of
/// cutting, because the chase state is entered fresh.
///
/// The field of view needs no handling either: <c>CameraCockpitState</c> sets <c>defaultFoV</c> on
/// entry and <c>defaultExternalFoV</c> on the way out, so neither the cinematic camera's own FoV nor
/// the cockpit's can leak into the other.
///
/// Flying is unaffected across the switch. The virtual joystick's deflection lives on a HUD
/// transform rather than in the camera, and <see cref="PatchHelper.EffectiveCameraMode"/> already
/// reports the cockpit in chase, so the stick keeps accumulating either side and the aircraft does
/// not twitch on the transition.
/// </summary>
public static class CockpitHold
{
    /// <summary>True while this is the reason the camera is in the cockpit.</summary>
    private static bool _held;

    public static void Poll()
    {
        if (!Plugin.Enabled.Value)
            return;

        if (Plugin.CockpitHoldActionId < 0)
            return;

        var player = GameManager.playerInput;
        if (player == null)
            return;

        bool down = player.GetButton(Plugin.CockpitHoldActionId);

        if (down)
        {
            if (!_held)
                TryEnter();

            return;
        }

        if (_held)
            TryLeave();
    }

    /// <summary>
    /// Guards are the ones <see cref="CameraToggle"/> applies, for the same reasons: the game's own
    /// "is the player flying" flag stands in for enumerating menus, the map and the radial menu; the
    /// camera picker means the player is already choosing a view; and the cockpit state reads
    /// <c>followingUnit</c> as an aircraft and takes its first pilot without checking either.
    ///
    /// Deliberately only arms from chase. From the orbit or free cameras the player is looking at
    /// something on purpose, and a key that yanks them into a cockpit they did not ask for is worse
    /// than one that does nothing.
    /// </summary>
    private static void TryEnter()
    {
        if (!GameManager.flightControlsEnabled)
            return;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState != cam.chaseState)
            return;

        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        if (picker != null && picker.isOpen)
            return;

        if (cam.followingUnit is not Aircraft aircraft
            || aircraft.pilots == null
            || aircraft.pilots.Length == 0
            || aircraft.pilots[0] == null
            || aircraft.pilots[0].dead)
        {
            return;
        }

        _held = true;
        cam.SwitchState(cam.cockpitState);
    }

    /// <summary>
    /// Releases the latch before anything else, so a switch that cannot happen leaves the key usable
    /// rather than stuck holding a camera it no longer owns.
    /// </summary>
    private static void TryLeave()
    {
        _held = false;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null)
            return;

        // Only take the camera back if it is still where this put it. Dying, ejecting, or the player
        // choosing another view while the key was down all move it, and dragging it back from
        // wherever it ended up would be worse than simply letting go.
        if (cam.currentState != cam.cockpitState)
            return;

        if (cam.followingUnit is not Aircraft)
            return;

        cam.SwitchState(cam.chaseState);
    }
}
