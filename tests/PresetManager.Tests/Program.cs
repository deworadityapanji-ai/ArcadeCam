using System.Reflection;
using ArcadeCam;
using ArcadeCam.Helpers;
using BepInEx.Configuration;

int checks = 0;
void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
string root = Path.Combine(Path.GetTempPath(), "ArcadeCam-preset-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string mainPath = Path.Combine(root, "ArcadeCam.cfg");
void Boot()
{
    var config = new ConfigFile(mainPath, false);
    Plugin.Instance = new Plugin { Config = config };
    foreach (var field in typeof(Plugin).GetFields(BindingFlags.Static | BindingFlags.Public))
    {
        if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(ConfigEntry<>)) continue;
        Type valueType = field.FieldType.GetGenericArguments()[0];
        object defaultValue = Activator.CreateInstance(valueType);
        if (field.Name == "EnableArcadeCam") defaultValue = true;
        var bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod
            && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(ConfigDefinition));
        string section = field.Name is "Preset" or "EnableArcadeCam" ? "ArcadeCam" : "Test Profile";
        field.SetValue(null, bind.MakeGenericMethod(valueType).Invoke(config,
            new[] { new ConfigDefinition(section, field.Name), defaultValue, new ConfigDescription("Test setting") }));
    }
    PresetManager.Initialize(root);
}
Boot();
Plugin.CameraDistanceScale.Value = 1.23f;
Plugin.CameraHeightOffset.Value = 4f;
Plugin.LeftInstruments.Value = HudAttachment.Hidden;
Plugin.Preset.Value = CameraPreset.AceCombat;
Check(Plugin.CenteringEnabled.Value && Plugin.CenteringForce.Value >= 0.9f, "Ace Combat applies strong centering");
Check(Plugin.SpeedMotionEnabled.Value && !Plugin.PitchFollowEnabled.Value, "Ace Combat speed motion applies");
Check(Math.Abs(Plugin.CenteringDelay.Value - 0.55f) < 0.001f, "Ace Combat applies the requested 55-percent delay");
Check(Plugin.Preset.Value == CameraPreset.AceCombat, "Applying a named preset does not recursively change it to Custom");
Plugin.Preset.Value = CameraPreset.AceCombat8;
Check(Plugin.PitchFollowEnabled.Value, "Ace Combat 8 applies pitch anticipation");
Check(Math.Abs(Plugin.CenteringDelay.Value - 0.55f) < 0.001f, "Ace Combat 8 applies the same delay");
Plugin.Preset.Value = CameraPreset.WarThunder;
Check(Plugin.LevelHorizonByDefault.Value && !Plugin.CenteringEnabled.Value, "War Thunder applies level horizon without centering");
Plugin.Preset.Value = CameraPreset.Custom;
Check(Math.Abs(Plugin.CameraDistanceScale.Value - 1.23f) < 0.001f, "Named preset hopping preserves saved Custom distance");
Check(Plugin.CameraHeightOffset.Value == 4f && Plugin.LeftInstruments.Value == HudAttachment.Hidden, "Custom restores geometry and hidden HUD choices");
Plugin.Preset.Value = CameraPreset.NuclearOptionPlus;
Check(!Plugin.LevelHorizonByDefault.Value && !Plugin.SpeedMotionEnabled.Value && !Plugin.PitchFollowEnabled.Value, "Plus clears additional motion");
Check(Plugin.LeftInstruments.Value == HudAttachment.Aircraft, "Plus restores aircraft-relative HUD");
Plugin.Preset.Value = CameraPreset.AceCombat8;
Plugin.CameraDistanceScale.Value = 0.67f;
Check(Plugin.Preset.Value == CameraPreset.AceCombat8 && Plugin.PitchFollowEnabled.Value, "Editing a named preset preserves its selected name and applied values");
Plugin.Preset.Value = CameraPreset.WarThunder;
Plugin.Preset.Value = CameraPreset.Custom;
Check(Math.Abs(Plugin.CameraDistanceScale.Value - 0.67f) < 0.001f && Plugin.PitchFollowEnabled.Value, "The edited Custom profile survives a preset roundtrip");
Plugin.CenteringForce.Value = 0.8f;
Plugin.CenteringEnabled.Value = false;
Check(Plugin.CenteringForce.Value == 0.8f, "Centering toggle preserves strength");
Boot();
Check(Plugin.Preset.Value == CameraPreset.Custom && Math.Abs(Plugin.CameraDistanceScale.Value - 0.67f) < 0.001f, "Custom survives a fresh config load");
Check(!Plugin.CenteringEnabled.Value && Plugin.CenteringForce.Value == 0.8f, "Centering state and strength survive restart independently");
Plugin.Preset.Value = CameraPreset.AceCombat8;
Boot();
Check(Plugin.Preset.Value == CameraPreset.AceCombat8 && Plugin.PitchFollowEnabled.Value, "Named presets survive restart");
Plugin.Preset.Value = CameraPreset.Custom;
Check(Math.Abs(Plugin.CameraDistanceScale.Value - 0.67f) < 0.001f, "Restarting in a named preset does not overwrite saved Custom");
Plugin.Preset.Value = CameraPreset.AceCombat8;
Plugin.CameraDistanceScale.Value = 0.71f;
Plugin.CameraHeightOffset.Value = -2f;
Plugin.PitchFollowStrength.Value = 0.28f;
Plugin.CenteringDelay.Value = 0.72f;
Plugin.CenteringEnabled.Value = false;
Check(Plugin.Preset.Value == CameraPreset.AceCombat8, "Multiple edits never force the selector back to Custom");
Check(Plugin.LeftInstruments.Value == HudAttachment.Camera && Plugin.SpeedMotionEnabled.Value, "Editing a preset does not restore unrelated old Custom values");
Boot();
Check(Plugin.Preset.Value == CameraPreset.AceCombat8 && Math.Abs(Plugin.CameraDistanceScale.Value - 0.71f) < 0.001f, "A modified named preset survives restart without reapplying factory values");
Check(Math.Abs(Plugin.CenteringDelay.Value - 0.72f) < 0.001f, "User-edited preset delay survives restart");
Check(Plugin.CameraHeightOffset.Value == -2f && Plugin.PitchFollowStrength.Value == 0.28f && !Plugin.CenteringEnabled.Value,
    "All modified preset settings survive restart together");
Plugin.Preset.Value = CameraPreset.WarThunder;
Plugin.Preset.Value = CameraPreset.Custom;
Check(Math.Abs(Plugin.CameraDistanceScale.Value - 0.71f) < 0.001f && Plugin.PitchFollowStrength.Value == 0.28f,
    "Custom explicitly restores the last edited setup after trying another preset");
Plugin.Preset.Value = CameraPreset.AceCombat8;
Check(Plugin.CameraDistanceScale.Value == 0.8f && Plugin.PitchFollowStrength.Value == 0.12f && Plugin.CenteringEnabled.Value,
    "Only explicitly choosing a preset again reapplies its starting values");
Check(Math.Abs(Plugin.CenteringDelay.Value - 0.55f) < 0.001f, "Explicit preset re-selection restores the new default delay");
Plugin.Preset.Value = CameraPreset.Custom;Plugin.TargetingScreenEnabled.Value = true;
Plugin.TargetingScreenXPercent.Value = 77f;
Plugin.TargetingScreenYPercent.Value = 23f;
Plugin.TargetingScreenWidthPercent.Value = 30f;
Plugin.Preset.Value = CameraPreset.AceCombat8;
Check(Plugin.TargetingScreenEnabled.Value && Plugin.TargetingScreenWidthPercent.Value == 30f, "Camera presets do not force a different target-screen size or visibility");
Plugin.Preset.Value = CameraPreset.Custom;
Check(Plugin.TargetingScreenXPercent.Value == 77f && Plugin.TargetingScreenYPercent.Value == 23f, "Custom restores target-screen placement");
Boot();
Check(Plugin.TargetingScreenEnabled.Value && Plugin.TargetingScreenWidthPercent.Value == 30f, "Target-screen visibility and size survive restart");
Check(Plugin.TargetingScreenXPercent.Value == 77f && Plugin.TargetingScreenYPercent.Value == 23f, "Target-screen position survives restart");
Plugin.Preset.Value = CameraPreset.Custom;
Plugin.Compass.Value = HudAttachment.Aircraft;
Plugin.Waterline.Value = HudAttachment.Hidden;
Plugin.WeaponCameraToggle.Value = true;
Plugin.WeaponCameraImpactSeconds.Value = 1.15f;
Plugin.WeaponCameraDistanceScale.Value = 1.4f;
Plugin.WeaponCameraFollowHitTarget.Value = true;
Plugin.WeaponCameraWreckageSeconds.Value = 2.5f;
Plugin.WeaponCameraImpactShake.Value = 0.2f;
Plugin.WeaponCameraHideHud.Value = true;
Plugin.Preset.Value = CameraPreset.AceCombat8;
Check(Plugin.LeftInstruments.Value == HudAttachment.Camera && Plugin.RightInstruments.Value == HudAttachment.Camera,
    "Ace presets still apply camera-fixed side instruments");
Check(Plugin.Compass.Value == HudAttachment.Aircraft && Plugin.Waterline.Value == HudAttachment.Hidden,
    "Applying a camera preset preserves independent compass/waterline choices");
Check(Plugin.WeaponCameraToggle.Value && Math.Abs(Plugin.WeaponCameraImpactSeconds.Value - 1.15f) < 0.001f,
    "Camera presets preserve weapon-view input and cinematic preferences");
Check(Plugin.WeaponCameraFollowHitTarget.Value && Plugin.WeaponCameraWreckageSeconds.Value == 2.5f
    && Plugin.WeaponCameraImpactShake.Value == 0.2f && Plugin.WeaponCameraHideHud.Value,
    "Camera presets preserve aftermath duration, shake and HUD-hiding preferences");
Plugin.Preset.Value = CameraPreset.WarThunder;
Check(Plugin.Compass.Value == HudAttachment.Aircraft && Plugin.Waterline.Value == HudAttachment.Hidden,
    "Trying another preset cannot silently force compass/waterline to Camera");
Boot();
Check(Plugin.Compass.Value == HudAttachment.Aircraft && Plugin.Waterline.Value == HudAttachment.Hidden,
    "Independent HUD attachment choices survive restart");
Check(Plugin.WeaponCameraToggle.Value && Math.Abs(Plugin.WeaponCameraDistanceScale.Value - 1.4f) < 0.001f,
    "Weapon camera preferences survive main-config restart");
Check(Plugin.WeaponCameraFollowHitTarget.Value && Plugin.WeaponCameraWreckageSeconds.Value == 2.5f
    && Plugin.WeaponCameraImpactShake.Value == 0.2f && Plugin.WeaponCameraHideHud.Value,
    "Aftermath, shake and HUD-hiding settings survive a main-config restart");
Plugin.Preset.Value = CameraPreset.Custom;
Check(Plugin.WeaponCameraToggle.Value && Math.Abs(Plugin.WeaponCameraImpactSeconds.Value - 1.15f) < 0.001f,
    "Custom restores saved weapon-view settings alongside the flight camera");
Check(Plugin.WeaponCameraFollowHitTarget.Value && Plugin.WeaponCameraWreckageSeconds.Value == 2.5f
    && Plugin.WeaponCameraImpactShake.Value == 0.2f && Plugin.WeaponCameraHideHud.Value,
    "Custom explicitly restores saved aftermath and HUD-hiding settings");
string custom = File.ReadAllText(Path.Combine(root, "ArcadeCam.custom.cfg"));
Check(!custom.Contains("EnableArcadeCam") && !custom.Contains("\nPreset ="), "Custom does not overwrite plugin ownership or preset selection");
Console.WriteLine($"PASS: {checks} checks using real BepInEx config serialization, saved profiles and restarts.");
Console.WriteLine("Temporary test configs: " + root);
