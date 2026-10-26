using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(CameraStateManager), nameof(CameraStateManager.SwitchState))]
internal static class CameraViewSwitchPatches
{
    [HarmonyPrefix]
    static bool Prefix(CameraStateManager __instance, ref CameraBaseState state)
    {
        if (__instance == null || state != __instance.orbitState)
            return true;

        // Vanilla maps Center to a chase/orbit switch. Recenter in place instead,
        // before any LeaveState/EnterState or third-party orbit placement runs.
        if (__instance.currentState == __instance.chaseState
            && ChaseInput.IsAllowed(__instance)
            && GameManager.playerInput.GetButtonDown("Center"))
        {
            CameraChaseStatePatches.RecenterView();
            return false;
        }

        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        bool localAircraft = __instance.followingUnit is Aircraft aircraft
                             && !aircraft.HasEjected()
                             && aircraft.pilots != null && aircraft.pilots.Length > 0
                             && aircraft.pilots[0] != null && !aircraft.pilots[0].dead
                             && GameManager.IsLocalAircraft(aircraft);
        bool fromFlightView = __instance.currentState == __instance.cockpitState
                              || __instance.currentState == __instance.freeState;
        if (ChaseEntryPolicy.PreferChase(
                Plugin.ChaseViewByDefault.Value, fromFlightView, localAircraft,
                picker != null && picker.isOpen,
                GameManager.gameState != GameState.Menu && GameManager.gameState != GameState.Editor))
        {
            state = __instance.chaseState;
        }

        return true;
    }
}