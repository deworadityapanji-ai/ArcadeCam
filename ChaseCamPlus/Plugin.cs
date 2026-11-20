using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam;

[BepInDependency(OriginalModCompatibility.OriginalGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(WtMouseAimCompatibility.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    public static Plugin Instance;
    private ChaseHudUpdater _hudUpdater;

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
    private const string SectionFreeLook = "Chase Free Look";
    private const string SectionTargetLook = "Chase Target Look";
    private const string SectionDefaults = "Chase Defaults";
    private const string SectionGeometry = "Chase Position";

    public static ConfigEntry<bool> EnableArcadeCam;
    public static ConfigEntry<CameraPreset> Preset;
    public static ConfigEntry<bool> WTMouseAimSupport;
    internal static bool IsActive { get; private set; }
    public static ConfigEntry<bool> CenteringEnabled;
    public static ConfigEntry<float> TargetReturnSeconds;
    public static ConfigEntry<bool> SpeedMotionEnabled;
    public static ConfigEntry<float> SpeedMotionStrength;
    public static ConfigEntry<bool> PitchFollowEnabled;
    public static ConfigEntry<float> PitchFollowStrength;
    public static ConfigEntry<float> PitchFollowLimit;
    public static ConfigEntry<float> MotionResponse;
    public static ConfigEntry<bool> DynamicChaseEnabled;
    public static ConfigEntry<bool> FrameRateIndependentChase;
    public static ConfigEntry<KillCamFrequency> KillCamFrequency;
    public static ConfigEntry<bool> KillCamIncludeGround;
    public static ConfigEntry<bool> KillCamOnMissileHit;
    public static ConfigEntry<KillCamView> KillCamView;
    public static ConfigEntry<float> KillCamSeconds;
    public static ConfigEntry<float> KillCamSlowMotion;
    public static ConfigEntry<bool> KillCamSound;
    public static ConfigEntry<float> KillCamSoundVolume;
    public static ConfigEntry<float> DynamicChaseStrength;
    public static ConfigEntry<float> DynamicChaseRollLag;
    public static ConfigEntry<float> DynamicChaseDeadzone;
    public static ConfigEntry<float> DynamicChaseResponse;
    public static ConfigEntry<bool> WeaponCameraEnabled;
    public static ConfigEntry<bool> WeaponCameraToggle;
    public static ConfigEntry<bool> WeaponCameraCinematic;
    public static ConfigEntry<float> WeaponCameraDistanceScale;
    public static ConfigEntry<float> WeaponCameraImpactSeconds;
    public static ConfigEntry<bool> WeaponCameraFollowHitTarget;
    public static ConfigEntry<float> WeaponCameraWreckageSeconds;
    public static ConfigEntry<float> WeaponCameraImpactShake;
    public static ConfigEntry<float> WeaponCameraReturnSeconds;
    public static ConfigEntry<float> WeaponCameraFieldOfView;
    public static ConfigEntry<bool> WeaponCameraHideHud;
    public static ConfigEntry<bool> WeaponCameraFollowAudio;
    public static ConfigEntry<bool> VirtualJoystick;

    // Stated as what you see, not as what is taken away. An option list that mixes "hide this" with
    // "keep that" makes the reader work out which way each true points; every one of these is on
    // when the thing is on screen.
    // The HUD settings (flight HUD in chase, map, instruments, targeting screen, layouts) moved to
    // the UI Plus mod, which imports them from this config on its first run.
    public static ConfigEntry<bool> TurretAim;

    public static ConfigEntry<bool> ChaseViewByDefault;
    public static ConfigEntry<bool> FreeLookOnChaseEntry;
    public static ConfigEntry<bool> LevelHorizonByDefault;
    public static ConfigEntry<LevelCameraStyle> LevelHorizonStyle;
    public static ConfigEntry<bool> CenterAfterTargetLook;
    public static ConfigEntry<bool> MoveCameraForTargetLook;
    public static ConfigEntry<float> CenteringForce;
    public static ConfigEntry<CenteringBehavior> CenteringMode;
    public static ConfigEntry<float> CenteringDelay;
    public static ConfigEntry<float> CenteringDeadzone;
    public static ConfigEntry<float> CameraDistanceScale;
    public static ConfigEntry<float> CameraHeightOffset;
    public static ConfigEntry<float> MinimumZoomFraction;
    public static ConfigEntry<bool> LookAtTargetInChase;
    public static ConfigEntry<bool> ToggleFreeLook;
    public static ConfigEntry<bool> KeepFreeLookAngle;
    public static ConfigEntry<float> FreeLookSensitivity;
    public static ConfigEntry<bool> FixWeaponFoV;
    public static ConfigEntry<bool> HitSoundOutsideCockpit;

    /// <summary>Rewired action names registered by <see cref="Patches.RewiredAwakePatches"/>.</summary>
    public const string ACTION_FREELOOK = "ArcadeCam::FreeLook";

    public const string ACTION_TOGGLE = "ArcadeCam::ToggleChase";

    public const string ACTION_COCKPIT_HOLD = "ArcadeCam::CockpitHold";

    public const string ACTION_CAMERA_ROLL = "ArcadeCam::CameraRollToggle";


    /// <summary>Ids assigned to the actions, or -1 if registration failed.</summary>
    public static int FreeLookActionId = -1;

    public static int ToggleActionId = -1;

    public static int CockpitHoldActionId = -1;

    public static int CameraRollActionId = -1;
    public const string ACTION_CENTERING_TOGGLE = "ArcadeCam::CenteringToggle";
    public static int CenteringToggleActionId = -1;
    public const string ACTION_WEAPON_CAMERA = "ArcadeCam::WeaponCamera";
    public static int WeaponCameraActionId = -1;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        EnableArcadeCam = Config.Bind("ArcadeCam", "EnableArcadeCam", true,
            "Enable ArcadeCam. Requires restarting the game. Off lets the original ChaseCamPlus control chase view if installed.");
        Preset = Config.Bind("ArcadeCam", "Preset", CameraPreset.Custom,
            "Apply a starting point when this selection changes. Edit any setting freely; the selected name and edited values survive restart. Custom restores your last saved user-edited setup.");
        WTMouseAimSupport = Config.Bind("Compatibility", "WTMouseAimSupport", false,
            "Opt-in camera/input bridge for WT Mouse Aim 0.68.0. WT keeps its mouse-flight instructor; ArcadeCam provides chase framing, HUD, free look and weapon view. Enable WT's own mouse aiming and CameraFollow too. Starts chase in aiming mode; ArcadeCam Free Look or WT's freeze binding looks around.");
        CenteringEnabled = Config.Bind(SectionFreeLook, "CenteringEnabled", false,
            "Enable automatic free-look centering without changing its strength. Also controlled by the ArcadeCam Centering Toggle binding. Target-look return uses its own setting.");
        TargetReturnSeconds = Config.Bind(SectionTargetLook, "TargetReturnSeconds", 0.45f,
            new ConfigDescription("Time to smoothly return from Look at Target or a Center View press, in seconds. 0 restores an instant return.", new AcceptableValueRange<float>(0f, 2f)));
        SpeedMotionEnabled = Config.Bind("Camera Motion", "SpeedMotionEnabled", false,
            "Add subtle boom movement with speed, acceleration and airbraking. Does not delay aircraft roll.");
        SpeedMotionStrength = Config.Bind("Camera Motion", "SpeedMotionStrength", 0.5f,
            new ConfigDescription("Strength of speed and acceleration boom movement. Ordinary acceleration has increased sensitivity; travel stays bounded.", new AcceptableValueRange<float>(0f, 1f)));
        PitchFollowEnabled = Config.Bind("Camera Motion", "PitchFollowEnabled", false,
            "Anticipate pitch maneuvers by opening space above/below the nose. Suspended during manual looking and target tracking.");
        PitchFollowStrength = Config.Bind("Camera Motion", "PitchFollowStrength", 0.12f,
            new ConfigDescription("Pitch anticipation strength. Higher reaches the selected angle limit at lower pitch rates.", new AcceptableValueRange<float>(0f, 0.5f)));
        PitchFollowLimit = Config.Bind("Camera Motion", "PitchFollowLimit", 8f,
            new ConfigDescription("Maximum pitch anticipation in degrees.", new AcceptableValueRange<float>(0f, 20f)));
        MotionResponse = Config.Bind("Camera Motion", "MotionResponse", 0.25f,
            new ConfigDescription("Smoothing time for boom motion and pitch anticipation, in seconds. Higher softens changes; aircraft roll remains immediate.", new AcceptableValueRange<float>(0.05f, 1f)));
        KillCamFrequency = Config.Bind("Kill Cam", "KillCamFrequency", Helpers.KillCamFrequency.Rare,
            "How often a kill plays the kill cam (the chase camera turns to watch the wreck, with slow motion in single player). Notable kills are likelier: gun kills, big or valuable targets, close kills, multi-kills; a carrier is near guaranteed. Always plays it for every aircraft/ship kill (handy for testing). Never plays with a missile inbound, near the ground, gear down or at high angle of attack. Skip with Fire or Center View.");
        KillCamView = Config.Bind("Kill Cam", "KillCamView", Helpers.KillCamView.Auto,
            "FromAircraft: the chase camera turns to watch the wreck from your aircraft. CutToWreck: cut to the wreck and orbit it, then return (you keep flying). Auto: cut for the big moments (close gun kills, big targets), watch otherwise.");
        KillCamOnMissileHit = Config.Bind("Kill Cam", "KillCamOnMissileHit", true,
            "Start the kill cam the moment your missile hits an aircraft, while it is still whole and breaking apart, instead of when the game later declares it dead (often seconds afterwards, by which time little is left). DESTROYED still waits for the actual kill. Gun kills always use the kill itself.");
        KillCamIncludeGround = Config.Bind("Kill Cam", "KillCamIncludeGround", false,
            "Also allow kill cams for ground vehicles and buildings.");
        KillCamSeconds = Config.Bind("Kill Cam", "KillCamSeconds", 2.2f,
            new ConfigDescription("How long the kill cam lasts, in real seconds.", new AcceptableValueRange<float>(0.5f, 6f)));
        KillCamSlowMotion = Config.Bind("Kill Cam", "KillCamSlowMotion", 0.3f,
            new ConfigDescription("Game speed during the kill cam, single player only (1 = no slow motion). Never overrides the game's own slow motion or the pause screen.", new AcceptableValueRange<float>(0.05f, 1f)));
        KillCamSound = Config.Bind("Kill Cam", "KillCamSound", true,
            "Play a sound when the kill cam starts: any .wav, .ogg or .mp3 placed in BepInEx/plugins/ArcadeCam/sounds/killcam (a random one each time). The folder is created on first use; no sounds are included.");
        KillCamSoundVolume = Config.Bind("Kill Cam", "KillCamSoundVolume", 0.8f,
            new ConfigDescription("Kill cam sound volume.", new AcceptableValueRange<float>(0f, 1f)));
        FrameRateIndependentChase = Config.Bind("Camera Motion", "FrameRateIndependentChase", true,
            "Smooth the chase camera's rotation the same way at any frame rate. The game's own smoothing changes with frame time, so uneven frames make the aircraft twitch a few degrees during fast rolls. Same smoothing amount (View Smoothing setting); off restores the game's original behaviour.");
        // New section and key names on purpose: the first attempt's keys may hold tuned-off values.
        DynamicChaseEnabled = Config.Bind("Dynamic Chase", "DynamicChaseEnabled", true,
            "Normal chase camera that leans into manoeuvres: pulling up raises the camera so the aircraft drops in the frame, rolling slides it to the opposite side, yawing lets it trail the turn. Aim stays locked to the aircraft (no lag), so landing stays easy. Keeps working during free look; pauses only for Look at Target. Not used with the level-horizon cameras. Also works alongside WT Mouse Aim.");
        DynamicChaseStrength = Config.Bind("Dynamic Chase", "DynamicChaseStrength", 2f,
            new ConfigDescription("How far the camera moves. 2 is the designed amount; 3 is exaggerated. Travel is always capped so the aircraft stays in frame.", new AcceptableValueRange<float>(0f, 3f)));
        DynamicChaseRollLag = Config.Bind("Dynamic Chase", "DynamicChaseRollLag", 0.3f,
            new ConfigDescription("How much the camera's roll trails the aircraft's roll (up to 8 degrees at 1). 0 keeps roll locked to the aircraft.", new AcceptableValueRange<float>(0f, 1f)));
        DynamicChaseDeadzone = Config.Bind("Dynamic Chase", "DynamicChaseDeadzone", 3f,
            new ConfigDescription("Rotation rates below this (degrees per second) are ignored, so small corrections and landing approaches stay steady.", new AcceptableValueRange<float>(0f, 30f)));
        DynamicChaseResponse = Config.Bind("Dynamic Chase", "DynamicChaseResponse", 0.7f,
            new ConfigDescription("Spring smoothing time in seconds. Lower is snappier, higher is floatier.", new AcceptableValueRange<float>(0.05f, 1.5f)));

        WeaponCameraEnabled = Config.Bind("Weapon Camera", "WeaponCameraEnabled", true,
            "Experimental player missile/bomb camera from chase view. Bind ArcadeCam Weapon Camera. Flight controls remain active; no spectator/unit switch is used.");
        WeaponCameraToggle = Config.Bind("Weapon Camera", "WeaponCameraToggle", false,
            "Off: hold the binding to follow your latest live missile/bomb. On: press to enter, press again to return. Returns after the weapon ends and any enabled impact view finishes.");
        WeaponCameraCinematic = Config.Bind("Weapon Camera", "WeaponCameraCinematic", true,
            "Use a smooth rear-quarter view with side changes responding to turns, plus an optional impact orbit. Off uses a simple trailing view.");
        WeaponCameraDistanceScale = Config.Bind("Weapon Camera", "WeaponCameraDistanceScale", 1f,
            new ConfigDescription("Scale the trailing distance relative to weapon size.", new AcceptableValueRange<float>(0.5f, 3f)));
        WeaponCameraImpactSeconds = Config.Bind("Weapon Camera", "WeaponCameraImpactSeconds", 0.75f,
            new ConfigDescription("Explosion-view duration when no damaged target can be followed. 0 skips all impact viewing, including wreckage and shake. Requires Cinematic. Release/press again always returns.", new AcceptableValueRange<float>(0f, 2f)));
        WeaponCameraFollowHitTarget = Config.Bind("Weapon Camera", "WeaponCameraFollowHitTarget", true,
            "After an armed detonation damages the missile's nearby target, follow that airframe or a substantial surviving part. Requires Cinematic and nonzero ImpactSeconds. An intact target may survive the hit; unavailable debris uses the explosion view.");
        WeaponCameraWreckageSeconds = Config.Bind("Weapon Camera", "WeaponCameraWreckageSeconds", 2f,
            new ConfigDescription("Seconds to follow the damaged target or wreckage after a hit. 0 disables target following. Releasing hold or pressing the toggle again returns immediately with the configured blend.", new AcceptableValueRange<float>(0f, 5f)));
        WeaponCameraImpactShake = Config.Bind("Weapon Camera", "WeaponCameraImpactShake", 0.65f,
            new ConfigDescription("Camera shake when the tracked missile/bomb has an armed explosion, including misses and terrain impacts. 0 disables it; 1 is maximum. Fades out in 0.4 seconds and affects only the rendered weapon view. Requires Cinematic and nonzero ImpactSeconds.", new AcceptableValueRange<float>(0f, 1f)));
        WeaponCameraReturnSeconds = Config.Bind("Weapon Camera", "WeaponCameraReturnSeconds", 0.35f,
            new ConfigDescription("Smooth return to the live aircraft camera, in seconds. 0 gives an immediate return.", new AcceptableValueRange<float>(0f, 1.5f)));
        WeaponCameraFieldOfView = Config.Bind("Weapon Camera", "WeaponCameraFieldOfView", 55f,
            new ConfigDescription("Field of view used only to render the weapon camera. The aircraft view keeps its own setting.", new AcceptableValueRange<float>(25f, 90f)));
        WeaponCameraHideHud = Config.Bind("Weapon Camera", "WeaponCameraHideHud", true,
            "Hide native flight instruments, combat HUD, helmet readouts and minimap throughout weapon tracking, impact/wreckage viewing and return. Restores each canvas's previous visibility afterward; flight controls and HUD updates remain active.");
        WeaponCameraFollowAudio = Config.Bind("Weapon Camera", "WeaponCameraFollowAudio", true,
            "Hear the world from the weapon-camera viewpoint, including native explosion delay and supersonic sound checks. Restores aircraft audio on return. No extra explosion sounds or volume boost are added.");
        VirtualJoystick = Config.Bind(
            SectionJoystick,
            "VirtualJoystick",
            true,
            "Mouse drives the virtual joystick in chase view, as it already does in the cockpit.");

        TurretAim = Config.Bind(
            SectionChaseHud,
            "TurretAim",
            true,
            "A manually aimed turret follows the camera in chase. Off pins it to the nose instead, "
            + "like a fixed forward gun.");
        ChaseViewByDefault = Config.Bind(
            SectionDefaults, "ChaseViewByDefault", true,
            "Use chase when the normal third-person switch would enter orbit from cockpit or a newly followed local aircraft. Explicit camera-picker choices are preserved.");
        FreeLookOnChaseEntry = Config.Bind(
            SectionDefaults, "FreeLookOnChaseEntry", true,
            "Start toggle free look automatically on entering chase. Hold mode still requires holding the binding. Applies on the next chase entry.");
        LevelHorizonByDefault = Config.Bind(
            SectionDefaults, "LevelHorizonByDefault", true,
            "Start with the selected level-horizon style on each chase entry. ArcadeCam Horizon Toggle switches between it and aircraft-relative chase while flying.");
        LevelHorizonStyle = Config.Bind(
            SectionDefaults, "LevelHorizonStyle", LevelCameraStyle.Cinematic,
            "Cinematic retains the drifting level-horizon camera. Orbit uses upright, aircraft-centered framing following horizontal travel, like vanilla orbit, with ArcadeCam controls and HUD. Orbit skips added speed/pitch motion. Applies immediately while the level horizon is active.");
        CenterAfterTargetLook = Config.Bind(
            SectionTargetLook, "CenterAfterTargetLook", true,
            "Return to the centered forward chase view when target look ends, instead of restoring the previous manual viewing angle. Applies immediately.");
        MoveCameraForTargetLook = Config.Bind(
            SectionTargetLook, "MoveCameraForTargetLook", true,
            "Move the camera across the aircraft from the target, keeping your aircraft between the camera and the enemy. Turn off for rotation-only target look. Applies immediately.");
        CenteringMode = Config.Bind(
            SectionFreeLook, "CenteringMode", CenteringBehavior.OnInputRelease,
            "OnInputRelease: center only after the mouse/look stick stops moving; full force snaps back. ConstantPull retains the previous behavior.");
        CenteringDelay = Config.Bind(
            SectionFreeLook, "CenteringDelay", 0.05f,
            new ConfigDescription("Idle time before release centering starts, in seconds. Helps avoid centering between mouse movement samples.",
                new AcceptableValueRange<float>(0f, 1f)));
        CenteringDeadzone = Config.Bind(
            SectionFreeLook, "CenteringDeadzone", 0.05f,
            new ConfigDescription("Extra look-stick deadzone for release detection. Mouse movement does not use this deadzone.",
                new AcceptableValueRange<float>(0f, 0.5f)));
        CameraDistanceScale = Config.Bind(
            SectionGeometry, "CameraDistanceScale", 1f,
            new ConfigDescription("Distance multiplier for either chase style. Less than 1 moves closer; more than 1 moves farther.",
                new AcceptableValueRange<float>(0.25f, 3f)));
        CameraHeightOffset = Config.Bind(
            SectionGeometry, "CameraHeightOffset", 0f,
            new ConfigDescription("Extra camera height in meters relative to the aircraft. Uses world up in level-horizon view.",
                new AcceptableValueRange<float>(-20f, 30f)));
        MinimumZoomFraction = Config.Bind(
            SectionGeometry, "MinimumZoomFraction", 0.25f,
            new ConfigDescription("Closest distance allowed by the game's existing Zoom View control, as a fraction of the normal boom. 0.25 permits quarter distance.",
                new AcceptableValueRange<float>(0.1f, 1f)));

        CenteringForce = Config.Bind(
            SectionFreeLook, "Centering Force", 0.8f,
            new ConfigDescription(
                "Centering strength while free look is active. 0 disables it. OnInputRelease at 1 snaps to center after input stops; ConstantPull always pulls. Applies immediately.",
                new AcceptableValueRange<float>(0f, 1f)));

        LookAtTargetInChase = Config.Bind(
            SectionTargetLook,
            "LookAtTargetInChase",
            true,
            "Hold the game's Look at Target binding to track the selected target in either chase camera. "
            + "Release behavior is controlled by CenterAfterTargetLook. Applies immediately.");

        ToggleFreeLook = Config.Bind(
            SectionFreeLook,
            "ToggleFreeLook",
            true,
            "Press ArcadeCam Free Look once to look around, then again to return the mouse to steering. "
            + "Turn off to use the original hold behavior. Menus and the map pause free look, then restore your toggle choice. Applies immediately.");
        KeepFreeLookAngle = Config.Bind(
            SectionFreeLook,
            "KeepFreeLookAngle",
            true,
            "Keep the last viewing angle when free look ends. Turn off to ease back to center. "
            + "Leaving chase view resets the angle. Applies immediately.");

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
        IsActive = EnableArcadeCam.Value;
        PresetManager.Initialize();
        if (IsActive)
        {
            OriginalModCompatibility.TakeControl(harmony);
            harmony.PatchAll();
            WtMouseAimCompatibility.Initialize(harmony);
            WTMouseAimSupport.SettingChanged += WtMouseAimCompatibility.SettingsChanged;
            WeaponCamera.Initialize(harmony);
            KillCam.Initialize(harmony);
            _hudUpdater = gameObject.AddComponent<ChaseHudUpdater>();
        }
        else
        {
            // Keep our independent binding names available without changing gameplay.
            harmony.CreateClassProcessor(typeof(Patches.RewiredAwakePatches)).Patch();
        }
    }

    private void OnDestroy()
    {
        if (WTMouseAimSupport != null) WTMouseAimSupport.SettingChanged -= WtMouseAimCompatibility.SettingsChanged;
        WtMouseAimCompatibility.Dispose();
        if (_hudUpdater != null) Destroy(_hudUpdater);
        WeaponCamera.Dispose();
    }

    private void Update()
    {
        if (!IsActive) return;
        WeaponCamera.Poll();
        CenteringToggle.Poll();
        ChaseFreeLook.Poll();
        CameraToggle.Poll();
        CinematicCamera.Poll();
        CockpitHold.Poll();
        HierarchyDump.Poll();
    }
}
