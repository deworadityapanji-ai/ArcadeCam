namespace ArcadeCam.Helpers;

/// <summary>Shared input guards for chase-camera features.</summary>
internal static class ChaseInput
{
    internal static bool IsAllowed(CameraStateManager cam)
    {
        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        return GameManager.playerInput != null
               && cam != null
               && cam.currentState == cam.chaseState
               && cam.followingUnit != null
               && GameManager.flightControlsEnabled
               && !DynamicMap.mapMaximized
               && !RadialMenuMain.IsInUse()
               && (picker == null || !picker.isOpen);
    }
}