using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ArcadeCam.Patches;

namespace ArcadeCam.Helpers;

internal static class PresetManager
{
    private static ConfigFile _custom;
    private static bool _applying;
    private static CameraPreset _previous;
    private static ConfigFile Config => Plugin.Instance.Config;
    private static bool IsProfile(ConfigDefinition definition) => definition.Section != "ArcadeCam";

    internal static void Initialize(string configDirectory = null)
    {
        string path = Path.Combine(configDirectory ?? Paths.ConfigPath, "ArcadeCam.custom.cfg");
        bool exists = File.Exists(path);
        _custom = new ConfigFile(path, false) { SaveOnConfigSet = false };
        _previous = Plugin.Preset.Value;
        if (!exists || _previous == CameraPreset.Custom) SaveCustom();
        // Named profiles are apply-on-selection starting points. The main config
        // already contains the user's edited values; never reapply defaults on boot.
        Config.SettingChanged += Changed;
    }

    private static void SaveCustom()
    {
        foreach (var setting in Config.Where(s => IsProfile(s.Key)))
            _custom.Bind(setting.Key, "", new ConfigDescription("Saved Custom profile")).Value =
                TomlTypeConverter.ConvertToString(setting.Value.BoxedValue, setting.Value.SettingType);
        _custom.Save();
    }

    private static void RestoreCustom()
    {
        foreach (var setting in Config.Where(s => IsProfile(s.Key)))
        {
            string fallback = TomlTypeConverter.ConvertToString(setting.Value.BoxedValue, setting.Value.SettingType);
            string value = _custom.Bind(setting.Key, fallback, new ConfigDescription("Saved Custom profile")).Value;
            try { setting.Value.BoxedValue = TomlTypeConverter.ConvertToValue(value, setting.Value.SettingType); }
            catch (Exception e) { Plugin.Logger.LogWarning($"Custom profile: could not restore {setting.Key}: {e.Message}"); }
        }
    }

    private static void Changed(object sender, SettingChangedEventArgs args)
    {
        if (_applying) return;
        if (args.ChangedSetting == Plugin.EnableArcadeCam)
        {
            Plugin.Logger.LogInfo("EnableArcadeCam changes take effect after restarting the game.");
            return;
        }
        if (args.ChangedSetting == Plugin.Preset)
        {
            if (_previous == CameraPreset.Custom) SaveCustom();
            _applying = true;
            bool save = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                if (Plugin.Preset.Value == CameraPreset.Custom) RestoreCustom();
                else ApplyValues(Plugin.Preset.Value);
                _previous = Plugin.Preset.Value;
            }
            finally { Config.SaveOnConfigSet = save; _applying = false; Config.Save(); }
            if (Plugin.IsActive)
            {
                CinematicCamera.SetEnabled(Plugin.LevelHorizonByDefault.Value);
                ChaseCameraMotion.Reset();
                var cam = SceneSingleton<CameraStateManager>.i;
                if (cam != null && cam.currentState == cam.chaseState)
                {
                    ChaseFreeLook.EnterChase();
                    CameraChaseStatePatches.RecenterView();
                }
            }
            return;
        }
        if (!IsProfile(args.ChangedSetting.Definition)) return;
        // Keep both the selected starting-point name and every currently displayed
        // value. Custom saves the edited setup, but is restored only on explicit selection.
        SaveCustom();
    }
    private static void ApplyValues(CameraPreset preset)
    {
        PresetValues p = PresetValues.For(preset);
        Plugin.ChaseViewByDefault.Value = true;
        Plugin.FreeLookOnChaseEntry.Value = true;
        Plugin.LevelHorizonByDefault.Value = p.LevelHorizon;
        Plugin.ToggleFreeLook.Value = true;
        Plugin.KeepFreeLookAngle.Value = true;
        Plugin.CenteringEnabled.Value = p.Centering;
        Plugin.CenteringForce.Value = p.Force;
        Plugin.CenteringMode.Value = CenteringBehavior.OnInputRelease;
        Plugin.CenteringDelay.Value = p.CenteringDelay;
        Plugin.CenteringDeadzone.Value = 0.05f;
        Plugin.CenterAfterTargetLook.Value = true;
        Plugin.MoveCameraForTargetLook.Value = true;
        Plugin.LookAtTargetInChase.Value = true;
        Plugin.TargetReturnSeconds.Value = 0.45f;
        Plugin.CameraDistanceScale.Value = p.Distance;
        Plugin.CameraHeightOffset.Value = 0f;
        Plugin.MinimumZoomFraction.Value = 0.25f;
        Plugin.SpeedMotionEnabled.Value = p.SpeedMotion;
        Plugin.SpeedMotionStrength.Value = 0.5f;
        Plugin.PitchFollowEnabled.Value = p.PitchFollow;
        Plugin.PitchFollowStrength.Value = 0.12f;
        Plugin.PitchFollowLimit.Value = 8f;
        Plugin.MotionResponse.Value = 0.25f;
        Plugin.FlightHud.Value = true;
        Plugin.Map.Value = true;
        Plugin.LeftInstruments.Value = p.Instruments;
        Plugin.RightInstruments.Value = p.Instruments;
        Plugin.PitchLadder.Value = false;
        Plugin.VirtualJoystickIndicator.Value = false;
    }
}