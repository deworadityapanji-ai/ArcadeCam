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

    // BepInEx writes config sections in alphabetical order and ignores the order they were bound in;
    // keys *within* a section do keep their bind order. So the order these are declared in controls
    // the file only within each section, and the sections themselves land wherever the alphabet puts
    // them. Named for what they are rather than sorted into a reading order on purpose — the numeric
    // prefixes that would force one were tried and judged not worth the noise in the file.
    private const string SectionConfig = "Config";
    private const string SectionJoystick = "Virtual Joystick";
    private const string SectionChaseHud = "Chase HUD";
    private const string SectionFreeLook = "Free Look";
    private const string SectionFeedback = "Feedback";
    private const string SectionCinematic = "Cinematic Chase Cam";
    private const string SectionCinematicHud = "Cinematic Chase HUD";
    private const string SectionDebug = "Debug";
    private const string SectionWorkarounds = "Workarounds";

    public static ConfigEntry<bool> Enabled;

    public static ConfigEntry<bool> VirtualJoystickInChase;

    public static ConfigEntry<bool> FlightHudInChase;
    public static ConfigEntry<bool> MapInChase;
    public static ConfigEntry<bool> TurretAimInChase;
    public static ConfigEntry<bool> HidePitchLadderInChase;
    public static ConfigEntry<bool> HideWaterlineInChase;
    public static ConfigEntry<bool> HideLeftInstrumentsInChase;
    public static ConfigEntry<bool> HideRightInstrumentsInChase;
    public static ConfigEntry<bool> HideCompassInChase;

    public static ConfigEntry<bool> FreeLook;
    public static ConfigEntry<float> FreeLookSensitivity;
    public static ConfigEntry<float> FreeLookMaxPitch;
    public static ConfigEntry<bool> RecenterOnRelease;
    public static ConfigEntry<bool> InvertFreeLookPitch;

    public static ConfigEntry<bool> HitSoundOutsideCockpit;

    public static ConfigEntry<bool> CinematicChaseCam;
    public static ConfigEntry<CinematicPreset> CinePreset;
    public static ConfigEntry<float> CineFov;
    public static ConfigEntry<float> CineBoomLag;
    public static ConfigEntry<float> CineAimLag;
    public static ConfigEntry<float> CinePositionSmoothing;
    public static ConfigEntry<float> CinePathLag;
    public static ConfigEntry<float> CineFlightPathAnchor;
    public static ConfigEntry<float> CineStretchPerG;
    public static ConfigEntry<float> CineDistance;
    public static ConfigEntry<float> CineHeight;
    public static ConfigEntry<float> CineFramingPitch;
    public static ConfigEntry<float> CineFramingLimit;
    public static ConfigEntry<float> CineSpeedPullback;
    public static ConfigEntry<float> CineSpeedLow;
    public static ConfigEntry<float> CineSpeedHigh;

    public static ConfigEntry<bool> CineFlightHudInChase;
    public static ConfigEntry<bool> CineMapInChase;
    public static ConfigEntry<bool> CineTurretAimInChase;
    public static ConfigEntry<bool> CineHidePitchLadderInChase;
    public static ConfigEntry<bool> CineHideWaterlineInChase;
    public static ConfigEntry<bool> CineHideLeftInstrumentsInChase;
    public static ConfigEntry<bool> CineHideRightInstrumentsInChase;
    public static ConfigEntry<bool> CineHideCompassInChase;

    public static ConfigEntry<bool> DebugDumpHudHierarchy;
    public static ConfigEntry<KeyCode> DebugDumpKey;

    public static ConfigEntry<bool> FixWeaponFoV;

    /// <summary>Rewired action names registered by <see cref="Patches.RewiredAwakePatches"/>.</summary>
    public const string ACTION_FREELOOK = "ChaseCamPlus::FreeLook";

    public const string ACTION_TOGGLE = "ChaseCamPlus::ToggleChase";

    public const string ACTION_CINEMATIC = "ChaseCamPlus::CinematicChaseCam";

    public const string ACTION_COCKPIT_HOLD = "ChaseCamPlus::CockpitHold";

    /// <summary>Ids assigned to the actions, or -1 if registration failed.</summary>
    public static int FreeLookActionId = -1;

    public static int ToggleActionId = -1;

    public static int CinematicActionId = -1;

    public static int CockpitHoldActionId = -1;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Enabled = Config.Bind(
            SectionConfig,
            "Enabled",
            true,
            "Master switch for the whole mod.");

        VirtualJoystickInChase = Config.Bind(
            SectionJoystick,
            "VirtualJoystickInChase",
            true,
            "Let the mouse drive the virtual joystick while the camera is in CHASE view. "
            + "Vanilla only accumulates mouse movement in the cockpit view, which leaves the stick "
            + "frozen (but still commanding) in chase.");

        FlightHudInChase = Config.Bind(
            SectionChaseHud,
            "FlightHudInChase",
            true,
            "Keep the flight HUD on in chase view. Vanilla switches it off on entering chase — "
            + "CameraChaseState.EnterState clears its showHUD flag, and the only thing that sets it "
            + "again is a debug key that works solely in the mission editor — so third person leaves "
            + "you with no instruments and no virtual joystick indicator. This restores the game's "
            + "own intent: the HUD appears in the camera positions it was written for (Back, Tail, "
            + "wing roots, Belly) and stays off in the ones where it would make no sense.");
        MapInChase = Config.Bind(
            SectionChaseHud,
            "MapInChase",
            true,
            "Keep the minimap alive in chase view. The game switches the DynamicMap object off "
            + "outside the cockpit, which on aircraft that carry a map leaves the frame drawn but "
            + "empty. Set false to leave the map off, which is what vanilla does. Requires "
            + "FlightHudInChase.");
        TurretAimInChase = Config.Bind(
            SectionChaseHud,
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
            SectionChaseHud,
            "HidePitchLadderInChase",
            true,
            "Hide the pitch ladder in chase view. The ladder's rungs and its horizon line are a "
            + "single scrolling texture, so this takes both — they cannot be separated by hiding "
            + "objects. Only applies while the HUD is shown in chase.");
        HideWaterlineInChase = Config.Bind(
            SectionChaseHud,
            "HideWaterlineInChase",
            true,
            "Hide the waterline — the fixed aircraft datum symbol at the centre of the HUD — in "
            + "chase view. Only applies while the HUD is shown in chase.");
        HideLeftInstrumentsInChase = Config.Bind(
            SectionChaseHud,
            "HideLeftInstrumentsInChase",
            false,
            "Hide the instrument cluster on the left of the frame in chase view: airspeed, angle of "
            + "attack and its indexer, Mach, g load and fuel. Off by default, unlike the pitch ladder "
            + "and waterline — these read correctly in third person, so they are only worth losing if "
            + "you want the frame clear.\n"
            + "Warnings are never hidden by this. The stall warning, the radar warning receiver and "
            + "the gear indicator share the same panel and stay.");
        HideRightInstrumentsInChase = Config.Bind(
            SectionChaseHud,
            "HideRightInstrumentsInChase",
            false,
            "Hide the instrument cluster on the right of the frame in chase view: throttle or "
            + "collective, rotor RPM on a helicopter, altitude, radar altitude and climb rate.");
        HideCompassInChase = Config.Bind(
            SectionChaseHud,
            "HideCompassInChase",
            false,
            "Hide the heading ribbon across the top of the frame in chase view, together with the "
            + "degree readout under it.");

        FreeLook = Config.Bind(
            SectionFreeLook,
            "FreeLook",
            true,
            "Enable the hold-to-orbit free look in CHASE view. Bind a key to 'Chase Cam Free Look' "
            + "under Flight Controls in the game's settings.");
        FreeLookSensitivity = Config.Bind(
            SectionFreeLook,
            "FreeLookSensitivity",
            1f,
            "Multiplier applied on top of the game's own View Sensitivity setting.");
        FreeLookMaxPitch = Config.Bind(
            SectionFreeLook,
            "FreeLookMaxPitch",
            85f,
            "Maximum vertical orbit angle in degrees. Kept below 90 to avoid the camera flipping "
            + "over the poles.");
        RecenterOnRelease = Config.Bind(
            SectionFreeLook,
            "RecenterOnRelease",
            true,
            "Return to the default chase angle when the free look key is released. "
            + "When false the camera keeps the angle you left it at.");
        InvertFreeLookPitch = Config.Bind(
            SectionFreeLook,
            "InvertFreeLookPitch",
            false,
            "Flip the vertical direction of the chase free look. Applied on top of the game's own "
            + "invert-pitch view setting, so the chase orbit can go one way while the cockpit free "
            + "look keeps going the other.");

        HitSoundOutsideCockpit = Config.Bind(
            SectionFeedback,
            "HitSoundOutsideCockpit",
            true,
            "Play the hit confirmation sound when your rounds land while the camera is outside the "
            + "cockpit. Vanilla gates both the hit marker and its sound on the cockpit view, so in "
            + "chase you get no feedback at all until the target explodes. Only the sound is added; "
            + "the on-screen marker stays cockpit-only. Follows the game's own 'show hit markers' "
            + "setting — turn that off and this goes quiet too.");

        CinematicChaseCam = Config.Bind(
            SectionCinematic,
            "CinematicChaseCam",
            true,
            "Enable the cinematic chase camera: the horizon stays level, the camera trails the "
            + "flight path instead of being welded to the airframe, and the aircraft is allowed to "
            + "rotate and drift inside the frame the way it would in front of a real one. Bind a key "
            + "to 'Cinematic Chase Cam' under Flight Controls to switch it in and out in flight — "
            + "with nothing bound this setting alone does nothing.\n"
            + "It is a viewing mode more than a flying one. The camera stops pointing where the "
            + "aircraft does, so anything read off it is unreliable; the HUD and turret behaviour it "
            + "gets are set separately below. Free look and the virtual joystick both keep working.");
        CinePreset = Config.Bind(
            SectionCinematic,
            "Preset",
            CinematicPreset.Manual,
            "Which set of camera dynamics to use.\n"
            + "Manual — the eight numbers below are used as written. This is the default.\n"
            + "Stable — calmer and easier to fly on. Trades drama for a steadier frame, mostly by "
            + "hanging the boom half off the nose instead of wholly off the flight path, which is "
            + "what stops a roll moving the camera.\n"
            + "Cinematic — the showy end: a lazy boom, a long leash, a camera that has to work to "
            + "keep up.\n"
            + "Stable and Cinematic ignore the eight numbers below entirely; each one lists the "
            + "values the two presets use, so you can copy them into Manual and adjust from there. "
            + "Presets never touch the rig or the framing — Fov, Distance, Height, FramingPitch and "
            + "the speed band stay yours in all three.");
        CineFov = Config.Bind(
            SectionCinematic,
            "Fov",
            30f,
            "Field of view for this mode, in degrees, easing in and out with it. Set 0 to leave the "
            + "game's own external FoV alone.\n"
            + "It gets its own rather than following the third-person setting because FramingPitch is "
            + "an angle: six degrees seats the aircraft 40% of the way down the frame at 30 and half "
            + "that at 60, so a wide view quietly undoes the composition the rest of these settings "
            + "were measured for. The FoV axis still works on top, clamped to the game's own 20-120.");
        CineBoomLag = Config.Bind(
            SectionCinematic,
            "BoomLag",
            0.55f,
            "Seconds the camera takes to swing round behind a change in the flight path. The single "
            + "most important setting here: it is what lets the aircraft rotate away from the camera "
            + "and be chased rather than followed. Small values approach the vanilla feel, large ones "
            + "leave the camera behind for a long time.\n"
            + "Preset values — Stable: 0.35, Cinematic: 0.80.");
        CineAimLag = Config.Bind(
            SectionCinematic,
            "AimLag",
            0.22f,
            "Seconds the camera takes to bring the aircraft back to where it is aiming. Wants to stay "
            + "well below BoomLag — the gap between the two is what makes the aircraft swing across "
            + "the frame and get caught. Set them equal and the camera merely feels soft.\n"
            + "Preset values — Stable: 0.15, Cinematic: 0.30.");
        CinePositionSmoothing = Config.Bind(
            SectionCinematic,
            "PositionSmoothing",
            0.18f,
            "Seconds the camera takes to settle into the position the boom asks for. This is ordinary "
            + "smoothing; the character comes from BoomLag, not from here.\n"
            + "Preset values — Stable: 0.12, Cinematic: 0.22.");
        CinePathLag = Config.Bind(
            SectionCinematic,
            "PathLag",
            0.25f,
            "Seconds of inertia the camera has in its own flight path — how slowly it accepts that "
            + "the aircraft has changed course. This is what lets the aircraft actually move around "
            + "inside the frame: at 0 the camera is pinned to the aircraft's position and the "
            + "aircraft can only ever spin on the spot, however everything else is set. The default "
            + "is deliberately restrained, enough to read as a camera rather than a mount. Raise it "
            + "for a looser, arcade-feeling camera the aircraft wanders across — but the size of that "
            + "wander is this multiplied by PositionSmoothing, so it grows faster than it looks and "
            + "0.6 with a long BoomLag is already a lot.\n"
            + "Preset values — Stable: 0.10, Cinematic: 0.45.");
        CineFlightPathAnchor = Config.Bind(
            SectionCinematic,
            "FlightPathAnchor",
            1f,
            "Where the boom hangs from: 1 anchors it to the flight path, 0 to the nose. The flight "
            + "path is what the reference footage shows — it is why the aircraft sits nose-up in "
            + "frame through a hard pull. Lower it towards 0 to bring the camera back to the vanilla "
            + "reference, which also stops an aileron roll moving the camera at all.\n"
            + "Preset values — Stable: 0.60, Cinematic: 1.00.");
        CineStretchPerG = Config.Bind(
            SectionCinematic,
            "StretchPerG",
            2f,
            "Metres the boom is stretched for every g the aircraft pulls, which is what makes it "
            + "shrink in a hard turn and swell again as the camera catches up. Only turning counts: "
            + "accelerating along the flight path takes the camera with it. 0 gives a boom of fixed "
            + "length.\n"
            + "Preset values — Stable: 1.0, Cinematic: 3.0.");
        CineDistance = Config.Bind(
            SectionCinematic,
            "Distance",
            1.35f,
            "Boom length as a multiple of the game's own chase distance, which is derived from the "
            + "aircraft's size.");
        CineHeight = Config.Bind(
            SectionCinematic,
            "Height",
            0.14f,
            "How far above the flight path the camera rides, as a fraction of the boom length. Sets "
            + "how much of the aircraft's top you see; it does not move it within the frame.");
        CineFramingPitch = Config.Bind(
            SectionCinematic,
            "FramingPitch",
            6f,
            "Degrees the camera aims above the aircraft, which is what seats it below the centre of "
            + "the frame. 0 centres it exactly.");
        CineFramingLimit = Config.Bind(
            SectionCinematic,
            "FramingLimit",
            0.75f,
            "How far the aircraft may drift from where the camera points before it is reined in, as a "
            + "fraction of the way to the top or bottom edge of the frame. A fraction rather than an "
            + "angle so it means the same thing at any FoV. Only ever removes excess, so below the "
            + "limit nothing about the feel changes; 1 lets the aircraft reach the edge, and is not "
            + "recommended with a long AimLag.\n"
            + "Preset values — Stable: 0.60, Cinematic: 0.88.");
        CineSpeedPullback = Config.Bind(
            SectionCinematic,
            "SpeedPullback",
            0.35f,
            "How much further back the camera goes when the aircraft is simply travelling fast, as a "
            + "fraction of the boom length added between SpeedLow and SpeedHigh. Reads a heavily "
            + "smoothed speed on purpose — this is about the aircraft's regime, not about what "
            + "happened in the last half second, and StretchPerG is what answers to manoeuvres. "
            + "0 disables it.\n"
            + "Preset values — Stable: 0.25, Cinematic: 0.45.");
        CineSpeedLow = Config.Bind(
            SectionCinematic,
            "SpeedLow",
            120f,
            "Speed in m/s at or below which the boom is at its base length.");
        CineSpeedHigh = Config.Bind(
            SectionCinematic,
            "SpeedHigh",
            350f,
            "Speed in m/s at or above which the boom is fully pulled back.");

        CineFlightHudInChase = Config.Bind(
            SectionCinematicHud,
            "FlightHudInChase",
            true,
            "The 'Chase HUD' settings all have a twin here, applied instead whenever the "
            + "cinematic camera is the one running, so third person and the cinematic view can have "
            + "different HUDs without touching the config between them.\n"
            + "Keep the flight HUD on in the cinematic view. Worth knowing before turning it on: this "
            + "camera deliberately stops pointing where the aircraft does, and the HUD is drawn "
            + "against the camera's own axes, so the velocity vector and anything else anchored to "
            + "the world will sit where the aircraft is not.");
        CineMapInChase = Config.Bind(
            SectionCinematicHud,
            "MapInChase",
            true,
            "Keep the minimap alive in the cinematic view. Requires FlightHudInChase in this section. "
            + "The map is a plan view and does not care where the camera is pointing, so it stays "
            + "readable here where the rest of the HUD may not.");
        CineTurretAimInChase = Config.Bind(
            SectionCinematicHud,
            "TurretAimInChase",
            true,
            "Let a manually aimed turret follow the camera in the cinematic view.\n"
            + "Expect this to aim badly, and not because of a bug: the turret is given the camera's "
            + "forward, and this camera's forward runs back towards the aircraft rather than out "
            + "ahead of it, so the turret ends up pointed at the ground below you. Set false to pin "
            + "it to the nose instead, like a fixed forward gun, which is the sane choice while this "
            + "camera is running.");
        CineHidePitchLadderInChase = Config.Bind(
            SectionCinematicHud,
            "HidePitchLadderInChase",
            true,
            "Hide the pitch ladder in the cinematic view. Its rungs and its horizon line are a single "
            + "scrolling texture, so this takes both. Only applies while the HUD is shown.");
        CineHideWaterlineInChase = Config.Bind(
            SectionCinematicHud,
            "HideWaterlineInChase",
            true,
            "Hide the waterline — the fixed aircraft datum symbol at the centre of the HUD — in the "
            + "cinematic view. Only applies while the HUD is shown.");
        CineHideLeftInstrumentsInChase = Config.Bind(
            SectionCinematicHud,
            "HideLeftInstrumentsInChase",
            false,
            "Hide the left instrument cluster in the cinematic view: airspeed, angle of attack and "
            + "its indexer, Mach, g load and fuel. Warnings and the gear indicator stay.");
        CineHideRightInstrumentsInChase = Config.Bind(
            SectionCinematicHud,
            "HideRightInstrumentsInChase",
            false,
            "Hide the right instrument cluster in the cinematic view: throttle or collective, rotor "
            + "RPM on a helicopter, altitude, radar altitude and climb rate.");
        CineHideCompassInChase = Config.Bind(
            SectionCinematicHud,
            "HideCompassInChase",
            false,
            "Hide the heading ribbon and its degree readout in the cinematic view.\n"
            + "Worth turning on here before anywhere else: this camera is free to point somewhere "
            + "other than the aircraft's heading, and a compass drawn across a view that is not "
            + "aligned with it is the most obviously wrong thing on the screen.");

        DebugDumpHudHierarchy = Config.Bind(
            SectionDebug,
            "DebugDumpHudHierarchy",
            false,
            "Diagnostic aid, normally off. When enabled, pressing DebugDumpKey writes the UI "
            + "hierarchy around the flight HUD to the BepInEx log. Which object owns a given HUD "
            + "widget is scene data rather than code, so this is the only way to name one.");
        DebugDumpKey = Config.Bind(
            SectionDebug,
            "DebugDumpKey",
            KeyCode.F10,
            "Key that triggers the hierarchy dump. Only read when DebugDumpHudHierarchy is true.");

        FixWeaponFoV = Config.Bind(
            SectionWorkarounds,
            "FixWeaponFoV",
            true,
            "Workaround for a vanilla bug: selecting an unguided bomb (and boresight zoom, when "
            + "'zoom on boresight' is on) forces the camera to the COCKPIT default FoV even in an "
            + "external view, and nothing puts it back. With this on, external views get "
            + "defaultExternalFoV instead, the way PlayerSettings already does it everywhere else. "
            + "ON by default — turn it off once the game ships a fix, so the mod stops "
            + "second-guessing the base game.");

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
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
