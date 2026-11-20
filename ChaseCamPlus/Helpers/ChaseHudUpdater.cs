using UnityEngine;

namespace ArcadeCam.Helpers;

// CameraStateManager runs at order 2. Finish after it, CombatHUD and other camera/HUD LateUpdates.
// The HUD itself is UI Plus's now (its updater runs right after this one, at 10001).
[DefaultExecutionOrder(10000)]
internal sealed class ChaseHudUpdater : MonoBehaviour
{
    private void LateUpdate()
    {
        if (!Plugin.IsActive) return;
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        KillCam.Tick(cam);
        WeaponCamera.Prepare(cam);
        WeaponCameraAudio.Tick(cam);
    }

    private void OnDestroy() => KillCam.Dispose();
}
