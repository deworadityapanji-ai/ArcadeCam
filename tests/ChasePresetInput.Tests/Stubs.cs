namespace ArcadeCam
{
    internal static class Plugin
    {
        internal static bool IsActive = true;
        internal static readonly BoolSetting WeaponCameraEnabled = new();
        internal static int WeaponCameraActionId = 5191;
        internal static readonly TestLog Logger = new();
    }
    internal sealed class BoolSetting { internal bool Value = true; }
    internal sealed class TestLog
    {
        internal int Warnings;
        internal void LogWarning(string text) { Warnings++; }
    }
}
namespace ArcadeCam.Helpers
{
    // Only the native IL rewrite/pure key policy runs here. Native input remains
    // a manual in-game test, not a fabricated Unity input stream.
    internal static class ChaseInput
    {
        internal static bool IsAllowed(CameraStateManager cam) => true;
    }
}
