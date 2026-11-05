using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Optional bridge: no compile-time WT reference and no changes to its DLL/config.
// Its instructor keeps ownership of flight commands; only view/input context is adapted.
internal static class WtMouseAimCompatibility
{
    internal const string Guid = "com.no.wtmouseaim";
    private static Func<Vector3> _aim;
    private static Func<Vector2> _look;
    private static ConfigEntry<bool> _enabled, _control, _follow, _rightClick, _invert, _overlay, _gloc;
    private static ConfigEntry<float> _sensitivity, _response;
    private static readonly List<MethodBase> Patched = new();
    private static Harmony _harmony;
    private static bool _ready, _layoutOk, _warned;

    internal static void Initialize(Harmony harmony)
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var plugin)) return;
        Version version = plugin.Metadata.Version;
        if (version.Major != 0 || version.Minor != 68 || version.Build != 0)
        { Disable($"WT Mouse Aim {version} has not been validated; the optional bridge remains off (tested: 0.68.0)."); return; }
        try
        {
            Assembly assembly = plugin.Instance.GetType().Assembly;
            Type rig = assembly.GetType("NuclearOptionMouseAim.AimRig", true);
            Type cfg = assembly.GetType("NuclearOptionMouseAim.Cfg", true);
            _enabled = Setting<bool>(cfg, "Enabled"); _control = Setting<bool>(cfg, "WriteControl");
            _follow = Setting<bool>(cfg, "CameraFollow"); _rightClick = Setting<bool>(cfg, "RightClickFreeze");
            _invert = Setting<bool>(cfg, "InvertPitch"); _overlay = Setting<bool>(cfg, "ShowOverlay");
            _gloc = Setting<bool>(cfg, "GLocFadeEnabled");
            _sensitivity = Setting<float>(cfg, "MouseSensitivity"); _response = Setting<float>(cfg, "OrbitAimSmoothing");
            _aim = AccessTools.MethodDelegate<Func<Vector3>>(AccessTools.PropertyGetter(rig, "AimForward"));
            _look = AccessTools.MethodDelegate<Func<Vector2>>(AccessTools.PropertyGetter(rig, "LookDelta"));
            _harmony = harmony; _layoutOk = true;
            MethodInfo update = AccessTools.Method(rig, "Update");
            MethodInfo gui = AccessTools.Method(plugin.Instance.GetType(), "OnGUI");
            MethodInfo frozen = AccessTools.Method(rig, "AimFrozen");
            if (update == null || gui == null || frozen == null) throw new MissingMethodException("WT view/input API");
            var rewrite = new HarmonyMethod(typeof(Patches.WtMouseAimPatches), nameof(Patches.WtMouseAimPatches.Transpiler));
            Patched.Add(update); harmony.Patch(update, transpiler: rewrite);
            Patched.Add(gui); harmony.Patch(gui, transpiler: rewrite);
            Patched.Add(frozen); harmony.Patch(frozen,
                postfix: new HarmonyMethod(typeof(WtMouseAimCompatibility), nameof(FreezeAimForArcadeView)));
            _ready = _layoutOk;
            if (!_ready) { Dispose(); return; }
            _enabled.SettingChanged += SourceSettingChanged;
            _control.SettingChanged += SourceSettingChanged;
            Plugin.Logger.LogInfo("WT Mouse Aim 0.68.0 bridge available. Enable WTMouseAimSupport under Compatibility to use it.");
        }
        catch (Exception error) { Dispose(); Disable("WT Mouse Aim bridge unavailable: " + error.Message); }
    }

    private static ConfigEntry<T> Setting<T>(Type cfg, string name) =>
        AccessTools.Field(cfg, name)?.GetValue(null) as ConfigEntry<T>
        ?? throw new MissingFieldException("WT Mouse Aim setting " + name);

    internal static bool OwnsChase(CameraStateManager cam) =>
        _ready && Plugin.IsActive && Plugin.WTMouseAimSupport.Value && _enabled.Value && _control.Value
        && cam != null && cam.currentState == cam.chaseState
        && GameManager.GetLocalAircraft(out Aircraft aircraft) && aircraft != null && !aircraft.disabled
        && !aircraft.HasEjected() && cam.followingUnit == aircraft;

    internal static bool OwnsChase() => _ready && Plugin.IsActive && Plugin.WTMouseAimSupport.Value
        && OwnsChase(SceneSingleton<CameraStateManager>.i);

    internal static CameraMode AimCameraMode() => OwnsChase() ? CameraMode.orbit : CameraStateManager.cameraMode;

    internal static bool NativeFreeLookHeld => OwnsChase()
        && ((GameManager.playerInput != null && GameManager.playerInput.GetButton("Free Look"))
            || (_rightClick.Value && Input.GetMouseButton(1)));

    internal static bool TryAim(CameraStateManager cam, out Vector3 aim, out float response)
    {
        aim = Vector3.zero; response = 10f;
        if (!OwnsChase(cam) || !_follow.Value) return false;
        try
        {
            aim = _aim(); response = Mathf.Max(0.1f, _response.Value);
            return aim.sqrMagnitude > 0.5f && !float.IsNaN(aim.x) && !float.IsNaN(aim.y) && !float.IsNaN(aim.z)
                && !float.IsInfinity(aim.x) && !float.IsInfinity(aim.y) && !float.IsInfinity(aim.z);
        }
        catch (Exception error) { Disable("WT aim read failed: " + error.Message); return false; }
    }

    internal static Vector2 FreeLookStep() => OwnsChase()
        ? Vector2.Scale(_look(), new Vector2(_sensitivity.Value, -_sensitivity.Value * (_invert.Value ? -1f : 1f)))
        : Vector2.zero;

    private static void FreezeAimForArcadeView(ref bool __result)
    {
        if (OwnsChase() && (ChaseFreeLook.IsActive() || ChaseTargetLook.IsActive()
            || ChaseViewReturn.IsActive || WeaponCamera.IsDisplaying)) __result = true;
    }

    internal static bool ShowOverlay() => _overlay.Value
        && !(OwnsChase() && WeaponCamera.IsDisplaying && Plugin.WeaponCameraHideHud.Value);

    internal static bool ShowGlocFade() => _gloc.Value && !OwnsChase();

    internal static void SettingsChanged(object sender, EventArgs args)
    {
        MouseAimView.Reset();
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState != cam.chaseState) return;
        ChaseFreeLook.EnterChase();
        Patches.CameraChaseStatePatches.RecenterView();
    }

    private static void SourceSettingChanged(object sender, EventArgs args)
    {
        if (Plugin.WTMouseAimSupport.Value) SettingsChanged(sender, args);
    }

    internal static void Disable(string reason)
    {
        _ready = _layoutOk = false;
        if (_warned) return;
        _warned = true;
        Plugin.Logger?.LogWarning(reason);
    }

    internal static void Dispose()
    {
        _ready = false;
        if (_enabled != null) _enabled.SettingChanged -= SourceSettingChanged;
        if (_control != null) _control.SettingChanged -= SourceSettingChanged;
        if (_harmony != null)
            foreach (MethodBase method in Patched) _harmony.Unpatch(method, HarmonyPatchType.All, _harmony.Id);
        Patched.Clear();
        MouseAimView.Reset();
    }
}
