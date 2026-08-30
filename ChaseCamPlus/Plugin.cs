using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ChaseCamPlus.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    public static Plugin Instance;

    // BepInEx writes config sections in alphabetical order and ignores the order they were bound in;
    // keys *within* a section do keep their bind order. So the order these are declared in controls
    // the file only within each section, and the sections themselves land wherever the alphabet puts
    // them. Named for what they are rather than sorted into a reading order on purpose — the numeric
    // prefixes that would force one were tried and judged not worth the noise in the file.
    // BepInEx writes config sections in alphabetical order and ignores the order they were bound in;
    // keys *within* a section do keep their bind order. So the order these are declared in controls
    // the file only within each section, and the sections themselves land wherever the alphabet puts
    // them.
    private const string SectionJoystick = "Virtual Joystick";
    private const string SectionChaseHud = "Chase HUD";
    private const string SectionOther = "Other";

    public static ConfigEntry<bool> VirtualJoystick;

    // Stated as what you see, not as what is taken away. An option list that mixes "hide this" with
    // "keep that" makes the reader work out which way each true points; every one of these is on
    // when the thing is on screen.
    public static ConfigEntry<bool> FlightHud;
    public static ConfigEntry<bool> Map;
    public static ConfigEntry<bool> TurretAim;
    public static ConfigEntry<bool> PitchLadder;
    public static ConfigEntry<bool> Waterline;
    public static ConfigEntry<bool> LeftInstruments;
    public static ConfigEntry<bool> RightInstruments;
    public static ConfigEntry<bool> Compass;
    public static ConfigEntry<bool> VirtualJoystickIndicator;

    public static ConfigEntry<float> FreeLookSensitivity;
    public static ConfigEntry<bool> FixWeaponFoV;
    public static ConfigEntry<bool> HitSoundOutsideCockpit;

    /// <summary>Rewired action names registered by <see cref="Patches.RewiredAwakePatches"/>.</summary>
    public const string ACTION_FREELOOK = "ChaseCamPlus::FreeLook";

    public const string ACTION_TOGGLE = "ChaseCamPlus::ToggleChase";

    public const string ACTION_COCKPIT_HOLD = "ChaseCamPlus::CockpitHold";

    public const string ACTION_CAMERA_ROLL = "ChaseCamPlus::CameraRollToggle";


    /// <summary>Ids assigned to the actions, or -1 if registration failed.</summary>
    public static int FreeLookActionId = -1;

    public static int ToggleActionId = -1;

    public static int CockpitHoldActionId = -1;

    public static int CameraRollActionId = -1;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        VirtualJoystick = Config.Bind(
            SectionJoystick,
            "VirtualJoystick",
            true,
            "Mouse drives the virtual joystick in chase view, as it already does in the cockpit.");

        FlightHud = Config.Bind(
            SectionChaseHud,
            "FlightHud",
            true,
            "Keep the flight HUD on in chase view. Vanilla switches it off on entering chase.");
        Map = Config.Bind(
            SectionChaseHud,
            "Map",
            true,
            "Keep the minimap alive in chase view. Needs FlightHud.");
        TurretAim = Config.Bind(
            SectionChaseHud,
            "TurretAim",
            true,
            "A manually aimed turret follows the camera in chase. Off pins it to the nose instead, "
            + "like a fixed forward gun.");
        PitchLadder = Config.Bind(
            SectionChaseHud,
            "PitchLadder",
            false,
            "Show the pitch ladder. Its rungs and its horizon line are one scrolling texture, so "
            + "they come and go together.");
        Waterline = Config.Bind(
            SectionChaseHud,
            "Waterline",
            false,
            "Show the waterline, the fixed aircraft datum at the centre of the HUD.");
        LeftInstruments = Config.Bind(
            SectionChaseHud,
            "LeftInstruments",
            false,
            "Show the left cluster: airspeed, angle of attack and its indexer, Mach, g load and fuel. "
            + "Warnings are never hidden by this.");
        RightInstruments = Config.Bind(
            SectionChaseHud,
            "RightInstruments",
            false,
            "Show the right cluster: throttle or collective, rotor RPM, altitude, radar altitude and "
            + "climb rate.");
        Compass = Config.Bind(
            SectionChaseHud,
            "Compass",
            false,
            "Show the heading ribbon across the top of the frame and the degree readout under it.");
        VirtualJoystickIndicator = Config.Bind(
            SectionChaseHud,
            "VirtualJoystickIndicator",
            false,
            "Show the virtual joystick's stretching line and the dot on its end. Drawing only — the "
            + "stick keeps working either way.");

        FreeLookSensitivity = Config.Bind(
            SectionOther,
            "FreeLookSensitivity",
            1f,
            "Multiplier on top of the game's own View Sensitivity, for the chase free look.");
        FixWeaponFoV = Config.Bind(
            SectionOther,
            "FixWeaponFoV",
            true,
            "Stop unguided bombs and boresight zoom forcing the cockpit field of view on external "
            + "views. Works around a vanilla bug; turn it off once the game ships a fix.");
        HitSoundOutsideCockpit = Config.Bind(
            SectionOther,
            "HitSoundOutsideCockpit",
            true,
            "Play the hit confirmation sound outside the cockpit, which vanilla gates on the cockpit "
            + "view. Works around an oversight; follows the game's own show-hit-markers setting.");

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }

    private void Update()
    {
        CameraToggle.Poll();
        CinematicCamera.Poll();
        CockpitHold.Poll();
        HierarchyDump.Poll();
    }
}
