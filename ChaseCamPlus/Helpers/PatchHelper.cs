using UnityEngine;

namespace ChaseCamPlus.Helpers;

public static class PatchHelper
{
    /// <summary>
    /// Substituted for the single <c>ldsfld CameraStateManager.cameraMode</c> inside
    /// <c>PilotPlayerState.PlayerAxisControls</c>, which vanilla compares against
    /// <see cref="CameraMode.cockpit"/> to decide whether to accumulate mouse movement into the
    /// virtual joystick. Reporting <see cref="CameraMode.cockpit"/> while actually in chase makes
    /// the game accumulate; reporting the real mode makes it skip, exactly as vanilla does.
    /// </summary>
    public static CameraMode EffectiveCameraMode()
    {
        CameraMode actual = CameraStateManager.cameraMode;

        if (!Plugin.Enabled.Value || !Plugin.VirtualJoystickInChase.Value)
            return actual;

        if (actual != CameraMode.chase)
            return actual;

        // The mouse is orbiting the camera right now, so leave the stick where it is.
        // This mirrors the vanilla "Free Look" freeze one branch further up the same method.
        if (FreeLookHeld())
            return actual;

        return CameraMode.cockpit;
    }

    /// <summary>
    /// Substituted for loads of <c>PlayerSettings.defaultFoV</c> in the weapon HUD states, which
    /// reach for the cockpit FoV without checking which camera is actually live. Mirrors the
    /// selection the game already makes in <c>PlayerSettings.ApplyOptions</c>:
    /// <c>currentState == cockpitState ? defaultFoV : defaultExternalFoV</c>.
    /// </summary>
    public static float CorrectDefaultFoV()
    {
        if (!Plugin.Enabled.Value || !Plugin.FixWeaponFoV.Value)
            return PlayerSettings.defaultFoV;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null)
            return PlayerSettings.defaultFoV;

        return cam.currentState == cam.cockpitState
            ? PlayerSettings.defaultFoV
            : PlayerSettings.defaultExternalFoV;
    }

    /// <summary>
    /// Substituted for the load of <c>CameraStateManager.cockpitState</c> inside
    /// <c>Turret.FixedUpdate</c>, which gates manual turret aiming on being in the cockpit:
    /// <c>if (aircraft.LocalSim &amp;&amp; i.currentState == i.cockpitState)</c>. Returning the
    /// current state instead makes that comparison true, so the turret keeps following the camera
    /// in chase as well.
    ///
    /// Following the camera is the right input even from behind the aircraft. The camera's forward
    /// is a direction, not a destination: it runs through the aircraft and on into the world beyond,
    /// which is the same direction the target designator marks at the centre of the screen. Aiming
    /// along it puts the turret where the designator is — the thing the player is looking at.
    ///
    /// It also removes the frozen-turret problem. Vanilla leaves <c>manualVector</c> at whatever it
    /// held when the cockpit was left, so a turret aimed off to one side stays there for good; once
    /// it tracks the camera, releasing free look recentres it exactly as the cockpit does.
    ///
    /// Restricted to chase on purpose. The free camera roams the map and the orbit camera swings
    /// wide, and either would drag the turret around with them.
    /// </summary>
    public static CameraBaseState AimAllowedState(CameraStateManager cam)
    {
        if (cam == null)
            return null;

        // Opened in chase whichever way TurretAimInChase is set: with it on the turret follows the
        // camera, with it off it still needs to be told to sit on the nose rather than be left
        // frozen. What the turret is actually given is decided in AimDirection.
        if (Plugin.Enabled.Value && cam.currentState == cam.chaseState)
            return cam.currentState;

        return cam.cockpitState;
    }

    /// <summary>
    /// Substituted for the camera's <c>forward</c> read that feeds the turret in
    /// <c>Turret.FixedUpdate</c>, so the direction can differ from the camera's in chase.
    ///
    /// With <c>TurretAimInChase</c> off the turret is pinned to the aircraft's nose, which turns it
    /// into a fixed forward gun for as long as you are in chase. That is a deliberate choice over
    /// simply leaving it alone: vanilla's frozen turret keeps whatever heading it held when the
    /// cockpit was left, and a weapon aimed somewhere you can neither see nor correct is worse than
    /// one aimed straight ahead.
    ///
    /// The cockpit is untouched in both cases — this only ever diverges while chase is the active
    /// camera.
    /// </summary>
    public static Vector3 AimDirection(Transform cameraTransform)
    {
        if (cameraTransform == null)
            return Vector3.forward;

        if (!Plugin.Enabled.Value)
            return cameraTransform.forward;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState != cam.chaseState)
            return cameraTransform.forward;

        if (Plugin.TurretAimInChase.Value)
            return cameraTransform.forward;

        return GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null
            ? aircraft.transform.forward
            : cameraTransform.forward;
    }

    /// <summary>True while the user holds the mod's own free look binding.</summary>
    public static bool FreeLookHeld()
    {
        if (!Plugin.Enabled.Value || !Plugin.FreeLook.Value)
            return false;

        if (Plugin.FreeLookActionId < 0)
            return false;

        var player = GameManager.playerInput;
        if (player == null)
            return false;

        return player.GetButton(Plugin.FreeLookActionId);
    }
}
