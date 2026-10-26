using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

// Restore only before a real native layout refresh. HUDAppManager keeps explicit
// app references, so data refreshes do not need the original parent every frame.
[HarmonyPatch(typeof(HUDAppManager), nameof(HUDAppManager.RefreshSettings))]
internal static class RestoreHudLayoutBeforeSettings
{
    [HarmonyPrefix]
    static void Prefix() => HudElements.RestoreAttachments();
}
