using System;
using System.Reflection;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(FloatingOrigin), nameof(FloatingOrigin.OriginShift))]
internal static class ChatOverlayOriginPatches
{
    private static readonly FieldInfo Threshold = AccessTools.Field(typeof(FloatingOrigin), "threshold");
    private static bool _reportedFailure;

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static void Prefix(FloatingOrigin __instance, Vector3 cameraPosition, out ChatOverlayOrigin __state)
    {
        __state = null;
        if (!Plugin.IsActive) return;
        try
        {
            // OriginShift is checked every frame. Allocate a snapshot only at
            // an actual shift, respecting the native instance's threshold.
            Vector3 viewPosition = cameraPosition;
            if (Threshold?.GetValue(__instance) is float threshold
                && Mathf.Abs(viewPosition.x) <= threshold && Mathf.Abs(viewPosition.y) <= threshold
                && Mathf.Abs(viewPosition.z) <= threshold) return;
            CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
            if (cam == null || cam.currentState != cam.chaseState
                || !GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null
                || cam.followingUnit != aircraft) return;
            __state = new ChatOverlayOrigin();
            __state.Capture();
        }
        catch (Exception error) { Report(error); }
    }

    [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
    internal static Exception Finalizer(Exception __exception, ChatOverlayOrigin __state)
    {
        // A finalizer covers partial/interrupted render paths too. It preserves
        // any native exception and never disables the world-origin operation.
        try { __state?.Restore(); }
        catch (Exception error) { Report(error); }
        return __exception;
    }

    private static void Report(Exception error)
    {
        if (_reportedFailure) return;
        _reportedFailure = true;
        Plugin.Logger.LogWarning("Chat overlay origin protection could not complete: " + error.Message);
    }
}
