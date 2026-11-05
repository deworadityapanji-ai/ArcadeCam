namespace ArcadeCam.Helpers;

internal static class GlocView
{
    // Used only by GLOC's presentation checks. Never change the global camera
    // mode: flight input, aircraft physics and other camera systems keep it.
    internal static CameraMode EffectiveCameraMode(GLOC source)
    {
        CameraMode actual = CameraStateManager.cameraMode;
        if (!Plugin.IsActive || actual != CameraMode.chase || source == null)
            return actual;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState != cam.chaseState
            || !GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null
            || cam.followingUnit != aircraft)
            return actual;

        Pilot pilot = source.GetComponent<Pilot>();
        return pilot != null && pilot.aircraft == aircraft ? CameraMode.cockpit : actual;
    }
}
