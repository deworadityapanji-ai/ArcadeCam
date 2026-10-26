namespace ArcadeCam.Helpers;

internal static class ChaseEntryPolicy
{
    internal static bool PreferChase(bool enabled, bool fromFlightView, bool localAircraft,
        bool explicitCameraSelection, bool playingScene) =>
        enabled && fromFlightView && localAircraft && !explicitCameraSelection && playingScene;
}