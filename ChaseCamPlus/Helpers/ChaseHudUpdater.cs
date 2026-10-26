using UnityEngine;

namespace ArcadeCam.Helpers;

// CameraStateManager runs at order 2. Finish after it, CombatHUD and other
// camera/HUD LateUpdates, including mods which defer target selection there.
[DefaultExecutionOrder(10000)]
internal sealed class ChaseHudUpdater : MonoBehaviour
{
    private void LateUpdate()
    {
        if (!Plugin.IsActive) return;
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam != null && cam.currentState == cam.chaseState
            && Plugin.FlightHud.Value && !DynamicMap.mapMaximized)
            HudElements.AttachToCamera(cam);
        else
            HudElements.RestoreAttachments();
        WeaponCamera.Prepare(cam);
        TargetingScreenOverlay.Tick(cam);
    }

    private void OnDestroy()
    {
        HudElements.Dispose();
        TargetingScreenOverlay.Dispose();
    }
}