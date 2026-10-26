using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.CheckOriginShift))]
internal static class WeaponViewOrigin
{
    [HarmonyPrefix]
    static bool Prefix(CameraStateManager __instance) => !WeaponCamera.ManagesOrigin(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.SwitchState))]
internal static class CancelWeaponViewOnCameraSwitch
{
    [HarmonyPrefix]
    static void Prefix(CameraStateManager __instance) => WeaponCamera.CancelFor(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.SetFollowingUnit))]
internal static class CancelWeaponViewOnUnitSwitch
{
    [HarmonyPrefix]
    static void Prefix(CameraStateManager __instance) => WeaponCamera.CancelFor(__instance);
}

[HarmonyPatch(typeof(CameraStateManager), "LateUpdate")]
internal static class RestoreWeaponViewBeforeCameraUpdate
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    static void Prefix(CameraStateManager __instance) => WeaponCamera.BeforeCameraUpdate(__instance);
}
