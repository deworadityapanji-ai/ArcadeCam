using BepInEx.Configuration;
using ArcadeCam.Helpers;
namespace ArcadeCam {
public class Plugin {
public static Plugin Instance;
public ConfigFile Config;
public static bool IsActive => false;
public static TestLog Logger = new();
public static ConfigEntry<bool> EnableArcadeCam;
public static ConfigEntry<CameraPreset> Preset;
public static ConfigEntry<bool> CenteringEnabled;
public static ConfigEntry<float> TargetReturnSeconds;
public static ConfigEntry<bool> SpeedMotionEnabled;
public static ConfigEntry<float> SpeedMotionStrength;
public static ConfigEntry<bool> PitchFollowEnabled;
public static ConfigEntry<float> PitchFollowStrength;
public static ConfigEntry<float> PitchFollowLimit;
public static ConfigEntry<float> MotionResponse;
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
public static ConfigEntry<bool> VirtualJoystick;
public static ConfigEntry<bool> TargetingScreenEnabled;
public static ConfigEntry<float> TargetingScreenXPercent;
public static ConfigEntry<float> TargetingScreenYPercent;
public static ConfigEntry<float> TargetingScreenWidthPercent;
public static ConfigEntry<bool> FlightHud;
public static ConfigEntry<bool> Map;
public static ConfigEntry<bool> TurretAim;
public static ConfigEntry<bool> PitchLadder;
public static ConfigEntry<HudAttachment> Waterline;
public static ConfigEntry<HudAttachment> LeftInstruments;
public static ConfigEntry<HudAttachment> RightInstruments;
public static ConfigEntry<HudAttachment> Compass;
public static ConfigEntry<bool> VirtualJoystickIndicator;
public static ConfigEntry<bool> ChaseViewByDefault;
public static ConfigEntry<bool> FreeLookOnChaseEntry;
public static ConfigEntry<bool> LevelHorizonByDefault;
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
public static ConfigEntry<bool> HitSoundOutsideCockpit;}
public class TestLog { public void LogInfo(string message) { } public void LogWarning(string message) { Console.WriteLine(message); } }
}
namespace ArcadeCam.Helpers {
internal static class CinematicCamera { internal static void SetEnabled(bool enabled) { } }
internal static class ChaseCameraMotion { internal static void Reset() { } }
internal static class ChaseFreeLook { internal static void EnterChase() { } }
}
namespace ArcadeCam.Patches { internal static class CameraChaseStatePatches { internal static void RecenterView() { } } }
public class CameraStateManager { public object currentState; public object chaseState; }
public static class SceneSingleton<T> { public static T i; }
