using System;
using ArcadeCam.Helpers;

namespace ArcadeCam;

/// <summary>
/// What ArcadeCam shares with other mods, UI Plus in particular. UI Plus finds this class by name
/// at runtime (it does not reference ArcadeCam's DLL), so keep these names and signatures stable.
/// </summary>
public static class ArcadeCamApi
{
    /// <summary>True while the weapon camera is on screen.</summary>
    public static bool WeaponCameraShowing => WeaponCamera.IsDisplaying;

    /// <summary>
    /// A combat callout: title (DESTROYED, HIT or MISS), the target's name, and whether it is an
    /// alert (shown in the alert colour).
    /// </summary>
    public static event Action<string, string, bool> Callout;

    internal static void RaiseCallout(string title, string name, bool alert)
    {
        try { Callout?.Invoke(title, name, alert); }
        catch (Exception e) { Plugin.Logger.LogWarning($"Callout listener failed: {e.Message}"); }
    }
}
