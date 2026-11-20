using System;

namespace ArcadeCam.Helpers;

public enum KillCamFrequency { Off, Rare, Normal, Often, Always }

/// <summary>Which kill cam to play: watch from your aircraft (B), cut to the wreck (A), or pick by score.</summary>
public enum KillCamView { Auto, FromAircraft, CutToWreck }

/// <summary>Mirrors the game's KillType values (Aircraft, Vehicle, Building, Missile, Ship).</summary>
internal enum KillKind { Aircraft = 0, Vehicle = 1, Building = 2, Missile = 3, Ship = 4 }

/// <summary>
/// How notable a kill is, and how likely it is to earn a kill cam. Pure math (no Unity types) so
/// it can be unit tested. Higher scores are rarer, more spectacular kills.
/// </summary>
internal static class KillCamScore
{
    /// <summary>At or above this score the kill cam always plays (unless switched off), e.g. a carrier.</summary>
    internal const int Guaranteed = 110;

    internal static bool Eligible(KillKind kind, bool includeGround) => kind switch
    {
        KillKind.Aircraft or KillKind.Ship => true,
        KillKind.Vehicle or KillKind.Building => includeGround,
        _ => false,   // shooting down missiles never qualifies
    };

    /// <param name="valueRatio">The victim's value divided by your own aircraft's value.</param>
    /// <param name="distance">Distance to the victim in metres.</param>
    /// <param name="multiKill">Another kill within the last few seconds.</param>
    internal static int Score(KillKind kind, bool gun, float valueRatio, float distance, bool multiKill)
    {
        int score = kind switch
        {
            KillKind.Aircraft => 40,
            KillKind.Ship => 60,
            KillKind.Vehicle => 15,
            KillKind.Building => 10,
            _ => 0,
        };
        if (gun) score += 35;                                  // guns are harder than missiles
        if (valueRatio > 1f)                                   // bigger, more valuable than you
            score += (int)Math.Round(Math.Min(60.0, 25.0 * Math.Log(valueRatio, 2.0)));
        if (distance < 1500f) score += 10;                     // close enough to see it go
        if (multiKill) score += 20;
        return score;
    }

    /// <summary>In Auto, scores at or above this cut to the wreck: gun kills up close, big targets.</summary>
    internal const int CutScore = 85;

    internal static bool CutsToWreck(KillCamView view, int score) =>
        view == KillCamView.CutToWreck || (view == KillCamView.Auto && score >= CutScore);

    /// <summary>Probability (0..1) that a kill with this score plays the kill cam.</summary>
    internal static float Chance(int score, KillCamFrequency frequency)
    {
        if (frequency == KillCamFrequency.Off) return 0f;
        if (frequency == KillCamFrequency.Always || score >= Guaranteed) return 1f;
        float scale = frequency switch
        {
            KillCamFrequency.Rare => 0.35f,
            KillCamFrequency.Normal => 0.7f,
            _ => 1f,
        };
        return Math.Max(0f, Math.Min(1f, score / 100f * scale));
    }

    /// <summary>Minimum real seconds between kill cams. Guaranteed kills only wait the shortest.</summary>
    internal static float Cooldown(KillCamFrequency frequency, int score) =>
        score >= Guaranteed || frequency == KillCamFrequency.Always ? 4f
        : frequency switch
        {
            KillCamFrequency.Rare => 45f,
            KillCamFrequency.Normal => 25f,
            _ => 12f,
        };
}
