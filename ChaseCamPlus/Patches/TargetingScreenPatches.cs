using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(TacScreen), nameof(TacScreen.Initialize))]
internal static class RegisterNativeTargetingDisplay
{
    [HarmonyPostfix]
    static void Postfix(TacScreen __instance, Aircraft aircraft) => TargetingScreenOverlay.Register(__instance, aircraft);
}

[HarmonyPatch(typeof(TacScreen), "OnDestroy")]
internal static class ForgetNativeTargetingDisplay
{
    [HarmonyPrefix]
    static void Prefix(TacScreen __instance, Aircraft ___aircraft) => TargetingScreenOverlay.Unregister(__instance, ___aircraft);
}