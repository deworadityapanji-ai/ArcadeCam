using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus.Helpers;

/// <summary>
/// Hides whole groups of flight instruments, leaving the parts of the HUD that are about the world
/// rather than about the aircraft — the reticles, the velocity vector, the free-look diamond, unit
/// markers and threat warnings.
///
/// Groups are matched on the **component type** rather than on object names, because the names are
/// scene data and vary by airframe while the types are code. The proof is in the game's own data: the
/// same throttle arc is called <c>throttleGauge_AB</c> on a fighter and <c>throttleGauge</c> on a
/// helicopter, and both carry a <c>ThrottleGauge</c>. Matching by name would have hidden it on one
/// aircraft and silently missed it on the other.
///
/// Type names are compared as strings rather than through <c>typeof</c> so that a game update which
/// renames or drops one of these classes costs that single element instead of failing to load the
/// mod. Nothing here is required for the mod to work.
///
/// Warnings are deliberately left out of every group. The stall and RPM warnings, the radar warning
/// receiver, the gear indicator and the helicopter's hover indicator all live in the same containers,
/// and tidying the frame is not worth being told about a stall too late.
/// </summary>
internal static class HudElements
{
    /// <summary>
    /// Airspeed, angle of attack, Mach, g and fuel — the cluster on the left of the frame.
    /// Fighter-only entries are simply absent on a helicopter and cost nothing.
    /// </summary>
    private static readonly string[] LeftTypes =
    {
        "SpeedGauge", "AoADisplay", "AoAIndexer", "MachIndicator", "GIndicators", "FuelGauge"
    };

    /// <summary>
    /// Throttle or collective, rotor RPM, altitude, radar altitude and climb rate — the cluster on
    /// the right.
    /// </summary>
    private static readonly string[] RightTypes =
    {
        "ThrottleGauge", "RPMGauge", "Altitude", "Climbrate"
    };

    /// <summary>The heading ribbon's degree readout. The ribbon itself is a field on the HUD.</summary>
    private static readonly string[] CompassTypes = { "Bearing" };

    private static readonly List<GameObject> Left = new();
    private static readonly List<GameObject> Right = new();
    private static readonly List<GameObject> Compass = new();

    private static Transform _root;
    private static FieldInfo _compassField;

    /// <summary>
    /// Applies the three groups. Called every frame from the chase postfix, like the rest of the HUD
    /// repair, so switching aircraft or camera mode is picked up without anything having to announce
    /// it. <c>SetActive</c> is only called when a group is actually in the wrong state.
    /// </summary>
    internal static void Apply(bool hideLeft, bool hideRight, bool hideCompass)
    {
        FlightHud hud = SceneSingleton<FlightHud>.i;
        if (hud == null)
        {
            _root = null;
            return;
        }

        if (_root != hud.transform || IsStale())
            Rescan(hud);

        Show(Left, !hideLeft);
        Show(Right, !hideRight);
        Show(Compass, !hideCompass);
    }

    /// <summary>Puts every group back, for leaving chase.</summary>
    internal static void ShowAll()
    {
        if (_root == null)
            return;

        Show(Left, true);
        Show(Right, true);
        Show(Compass, true);
    }

    private static void Show(List<GameObject> group, bool show)
    {
        foreach (GameObject element in group)
        {
            if (element != null && element.activeSelf != show)
                element.SetActive(show);
        }
    }

    /// <summary>
    /// True once anything collected has been destroyed — which is what changing aircraft does, since
    /// the instrument cluster is a per-airframe prefab (<c>Fighter1_HUDExtras</c>,
    /// <c>AttackHelo1_HUDExtras</c>, and so on) instantiated under the shared canvas.
    /// </summary>
    private static bool IsStale()
    {
        foreach (GameObject element in Left)
            if (element == null) return true;

        foreach (GameObject element in Right)
            if (element == null) return true;

        foreach (GameObject element in Compass)
            if (element == null) return true;

        return false;
    }

    private static void Rescan(FlightHud hud)
    {
        _root = hud.transform;

        Left.Clear();
        Right.Clear();
        Compass.Clear();

        // Inactive children included: a gauge the game has switched off for this airframe must still
        // be collected, or turning the group back on would leave it behind.
        foreach (Transform node in _root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                string type = component.GetType().Name;

                if (Contains(LeftTypes, type))
                    Add(Left, node.gameObject);
                else if (Contains(RightTypes, type))
                    Add(Right, node.gameObject);
                else if (Contains(CompassTypes, type))
                    Add(Compass, node.gameObject);
            }
        }

        // The ribbon has no component of its own — it is a RawImage held in a private field on the
        // HUD, the same shape as the waterline and the pitch ladder. Taking the GameObject rather
        // than the RawImage's `enabled` flag means its heading marker goes with it.
        _compassField ??= AccessTools.Field(typeof(FlightHud), "compass");
        if (_compassField?.GetValue(hud) is Component ribbon)
            Add(Compass, ribbon.gameObject);

        Plugin.Logger.LogInfo(
            $"HUD element groups resolved: {Left.Count} left, {Right.Count} right, "
            + $"{Compass.Count} compass.");
    }

    private static void Add(List<GameObject> group, GameObject element)
    {
        if (!group.Contains(element))
            group.Add(element);
    }

    private static bool Contains(string[] types, string name)
    {
        foreach (string type in types)
            if (type == name) return true;

        return false;
    }
}
