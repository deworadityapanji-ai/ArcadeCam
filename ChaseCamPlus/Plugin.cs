using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChaseCamPlus.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus;

public static class PluginInfo
{
    public const string PLUGIN_GUID = "ChaseCamPlus";
    public const string PLUGIN_NAME = "ChaseCamPlus";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    public static Plugin Instance;

    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> VirtualJoystickInChase;
    public static ConfigEntry<bool> FreeLook;
    public static ConfigEntry<float> FreeLookSensitivity;
    public static ConfigEntry<float> FreeLookMaxPitch;
    public static ConfigEntry<bool> RecenterOnRelease;
    public static ConfigEntry<bool> InvertFreeLookPitch;
    public static ConfigEntry<bool> FlightHudInChase;
    public static ConfigEntry<bool> MapInChase;
    public static ConfigEntry<bool> TurretAimInChase;
    public static ConfigEntry<bool> HidePitchLadderInChase;
    public static ConfigEntry<bool> HideWaterlineInChase;
    public static ConfigEntry<bool> DebugDumpHudHierarchy;
    public static ConfigEntry<KeyCode> DebugDumpKey;
    public static ConfigEntry<bool> FixWeaponFoV;
    public static ConfigEntry<bool> HitSoundOutsideCockpit;

    /// <summary>Rewired action names registered by <see cref="Patches.RewiredAwakePatches"/>.</summary>
    public const string ACTION_FREELOOK = "ChaseCamPlus::FreeLook";

    public const string ACTION_TOGGLE = "ChaseCamPlus::ToggleChase";

    /// <summary>Ids assigned to the actions, or -1 if registration failed.</summary>
    public static int FreeLookActionId = -1;

    public static int ToggleActionId = -1;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Enabled = Config.Bind(
            "Config",
            "Enabled",
            true,
            "Master switch for the whole mod.");
        VirtualJoystickInChase = Config.Bind(
            "Virtual Joystick",
            "VirtualJoystickInChase",
            true,
            "Let the mouse drive the virtual joystick while the camera is in CHASE view. "
            + "Vanilla only accumulates mouse movement in the cockpit view, which leaves the stick "
            + "frozen (but still commanding) in chase.");
        FreeLook = Config.Bind(
            "Free Look",
            "FreeLook",
            true,
            "Enable the hold-to-orbit free look in CHASE view. Bind a key to 'Chase Cam Free Look' "
            + "under Flight Controls in the game's settings.");
        FreeLookSensitivity = Config.Bind(
            "Free Look",
            "FreeLookSensitivity",
            1f,
            "Multiplier applied on top of the game's own View Sensitivity setting.");
        FreeLookMaxPitch = Config.Bind(
            "Free Look",
            "FreeLookMaxPitch",
            85f,
            "Maximum vertical orbit angle in degrees. Kept below 90 to avoid the camera flipping "
            + "over the poles.");
        RecenterOnRelease = Config.Bind(
            "Free Look",
            "RecenterOnRelease",
            true,
            "Return to the default chase angle when the free look key is released. "
            + "When false the camera keeps the angle you left it at.");
        InvertFreeLookPitch = Config.Bind(
            "Free Look",
            "InvertFreeLookPitch",
            false,
            "Flip the vertical direction of the chase free look. Applied on top of the game's own "
            + "invert-pitch view setting, so the chase orbit can go one way while the cockpit free "
            + "look keeps going the other.");
        FixWeaponFoV = Config.Bind(
            "Workarounds",
            "FixWeaponFoV",
            true,
            "Workaround for a vanilla bug: selecting an unguided bomb (and boresight zoom, when "
            + "'zoom on boresight' is on) forces the camera to the COCKPIT default FoV even in an "
            + "external view, and nothing puts it back. With this on, external views get "
            + "defaultExternalFoV instead, the way PlayerSettings already does it everywhere else. "
            + "ON by default — turn it off once the game ships a fix, so the mod stops "
            + "second-guessing the base game.");
        HitSoundOutsideCockpit = Config.Bind(
            "Feedback",
            "HitSoundOutsideCockpit",
            true,
            "Play the hit confirmation sound when your rounds land while the camera is outside the "
            + "cockpit. Vanilla gates both the hit marker and its sound on the cockpit view, so in "
            + "chase you get no feedback at all until the target explodes. Only the sound is added; "
            + "the on-screen marker stays cockpit-only. Follows the game's own 'show hit markers' "
            + "setting — turn that off and this goes quiet too.");

        FlightHudInChase = Config.Bind(
            "Chase HUD",
            "FlightHudInChase",
            true,
            "Keep the flight HUD on in chase view. Vanilla switches it off on entering chase — "
            + "CameraChaseState.EnterState clears its showHUD flag, and the only thing that sets it "
            + "again is a debug key that works solely in the mission editor — so third person leaves "
            + "you with no instruments and no virtual joystick indicator. This restores the game's "
            + "own intent: the HUD appears in the camera positions it was written for (Back, Tail, "
            + "wing roots, Belly) and stays off in the ones where it would make no sense.");
        MapInChase = Config.Bind(
            "Chase HUD",
            "MapInChase",
            true,
            "Keep the minimap alive in chase view. The game switches the DynamicMap object off "
            + "outside the cockpit, which on aircraft that carry a map leaves the frame drawn but "
            + "empty. Set false to leave the map off, which is what vanilla does. Requires "
            + "FlightHudInChase.");

        TurretAimInChase = Config.Bind(
            "Chase HUD",
            "TurretAimInChase",
            true,
            "Let a manually aimed turret follow the camera in chase view, as it already does in the "
            + "cockpit. Vanilla gates that on the cockpit camera, so in chase the turret holds "
            + "whatever heading it was last given and cannot be moved — aim off to one side, switch "
            + "to chase, and it stays there. With this on it tracks the camera and recentres when "
            + "you release free look, matching cockpit behaviour. Chase only: the free and orbit "
            + "cameras would drag the turret around with them.\n"
            + "With this off the turret is instead pinned to the aircraft's nose while in chase, "
            + "making it behave like a fixed forward gun — still better than vanilla, which leaves "
            + "it aimed wherever the cockpit left it with no way to move it.");
        HidePitchLadderInChase = Config.Bind(
            "Chase HUD",
            "HidePitchLadderInChase",
            true,
            "Hide the pitch ladder in chase view. The ladder's rungs and its horizon line are a "
            + "single scrolling texture, so this takes both — they cannot be separated by hiding "
            + "objects. Only applies while the HUD is shown in chase.");
        HideWaterlineInChase = Config.Bind(
            "Chase HUD",
            "HideWaterlineInChase",
            true,
            "Hide the waterline — the fixed aircraft datum symbol at the centre of the HUD — in "
            + "chase view. Only applies while the HUD is shown in chase.");

        DebugDumpHudHierarchy = Config.Bind(
            "Debug",
            "DebugDumpHudHierarchy",
            false,
            "Diagnostic aid, normally off. When enabled, pressing DebugDumpKey writes the UI "
            + "hierarchy around the flight HUD to the BepInEx log. Which object owns a given HUD "
            + "widget is scene data rather than code, so this is the only way to name one.");
        DebugDumpKey = Config.Bind(
            "Debug",
            "DebugDumpKey",
            KeyCode.F10,
            "Key that triggers the hierarchy dump. Only read when DebugDumpHudHierarchy is true.");

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }

    private void Update()
    {
        CameraToggle.Poll();
        HierarchyDump.Poll();
    }
}
