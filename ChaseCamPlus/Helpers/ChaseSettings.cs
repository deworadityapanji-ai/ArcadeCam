namespace ChaseCamPlus.Helpers;

/// <summary>
/// Picks between the two sets of chase-view settings — the ordinary one and the cinematic camera's
/// twin — so every call site asks what it should do now rather than which camera is running.
///
/// The two views want genuinely different answers. The ordinary chase camera points where the
/// aircraft points, so the HUD drawn on it reads correctly and a turret slaved to it aims where you
/// are looking. The cinematic camera deliberately does neither, and someone who wants instruments in
/// one of them may well not want them in the other.
/// </summary>
internal static class ChaseSettings
{
    private static bool Cinematic => CinematicCamera.Active;

    internal static bool FlightHud =>
        Cinematic ? Plugin.CineFlightHudInChase.Value : Plugin.FlightHudInChase.Value;

    internal static bool Map =>
        Cinematic ? Plugin.CineMapInChase.Value : Plugin.MapInChase.Value;

    internal static bool TurretFollowsCamera =>
        Cinematic ? Plugin.CineTurretAimInChase.Value : Plugin.TurretAimInChase.Value;

    internal static bool HidePitchLadder =>
        Cinematic ? Plugin.CineHidePitchLadderInChase.Value : Plugin.HidePitchLadderInChase.Value;

    internal static bool HideWaterline =>
        Cinematic ? Plugin.CineHideWaterlineInChase.Value : Plugin.HideWaterlineInChase.Value;

    internal static bool HideLeftInstruments =>
        Cinematic ? Plugin.CineHideLeftInstrumentsInChase.Value : Plugin.HideLeftInstrumentsInChase.Value;

    internal static bool HideRightInstruments =>
        Cinematic ? Plugin.CineHideRightInstrumentsInChase.Value : Plugin.HideRightInstrumentsInChase.Value;

    internal static bool HideCompass =>
        Cinematic ? Plugin.CineHideCompassInChase.Value : Plugin.HideCompassInChase.Value;
}
