using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using Rewired;

namespace ArcadeCam.Helpers;

internal static class OriginalModCompatibility
{
    internal const string OriginalGuid = "ChaseCamPlus";

    internal static void TakeControl(Harmony harmony)
    {
        if (!Chainloader.PluginInfos.TryGetValue(OriginalGuid, out var original)) return;
        // A soft dependency ensures the original has installed its patches first.
        // Keep its input registration so its own bindings are still visible and saved.
        var inputAwake = AccessTools.Method(typeof(InputManager_Base), "Awake");
        foreach (var method in Harmony.GetAllPatchedMethods().ToArray())
        {
            if (method == inputAwake) continue;
            var info = Harmony.GetPatchInfo(method);
            if (info != null && info.Owners.Contains(OriginalGuid))
                harmony.Unpatch(method, HarmonyPatchType.All, OriginalGuid);
        }
        var update = AccessTools.Method(original.Instance.GetType(), "Update");
        if (update != null)
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(OriginalModCompatibility), nameof(SuppressOriginalUpdate)));
        Plugin.Logger.LogInfo("ArcadeCam is controlling chase view. ChaseCamPlus remains installed; disable EnableArcadeCam and restart to use it.");
    }

    private static bool SuppressOriginalUpdate() => false;
}