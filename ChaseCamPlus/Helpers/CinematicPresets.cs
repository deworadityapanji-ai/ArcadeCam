namespace ChaseCamPlus.Helpers;

/// <summary>Which set of cinematic camera dynamics is in force.</summary>
public enum CinematicPreset
{
    /// <summary>Use the numbers in the config file.</summary>
    Manual,

    /// <summary>Calm and easy to fly on. Trades away some of the drama for a steadier frame.</summary>
    Stable,

    /// <summary>The showy end: a lazy boom, a long leash, and a camera that has to work to keep up.</summary>
    Cinematic
}

/// <summary>
/// Resolves the cinematic camera's eight behaviour settings against the chosen preset.
///
/// Only the settings that decide how the camera *behaves* are covered. The rig and the composition —
/// field of view, boom length, ride height, framing pitch, the speed band — stay under the player's
/// control in every preset, because they are taste and screen size rather than degrees of drama, and
/// having a preset quietly re-frame the shot would be a nasty surprise.
///
/// The preset values are duplicated in each setting's config description, so the file explains itself
/// without the mod having to overwrite the numbers the player typed. Keep the two in step.
/// </summary>
internal static class CinematicPresets
{
    //                                                  manual                        stable  cinematic
    internal static float BoomLag           => Pick(Plugin.CineBoomLag.Value,           0.35f,     0.80f);
    internal static float AimLag            => Pick(Plugin.CineAimLag.Value,            0.15f,     0.30f);
    internal static float PositionSmoothing => Pick(Plugin.CinePositionSmoothing.Value, 0.12f,     0.22f);
    internal static float PathLag           => Pick(Plugin.CinePathLag.Value,           0.10f,     0.45f);
    internal static float FlightPathAnchor  => Pick(Plugin.CineFlightPathAnchor.Value,  0.60f,     1.00f);
    internal static float StretchPerG       => Pick(Plugin.CineStretchPerG.Value,       1.00f,     3.00f);
    internal static float SpeedPullback     => Pick(Plugin.CineSpeedPullback.Value,     0.25f,     0.45f);
    internal static float FramingLimit      => Pick(Plugin.CineFramingLimit.Value,      0.60f,     0.88f);

    private static float Pick(float manual, float stable, float cinematic)
    {
        return Plugin.CinePreset.Value switch
        {
            CinematicPreset.Stable => stable,
            CinematicPreset.Cinematic => cinematic,
            _ => manual
        };
    }
}
