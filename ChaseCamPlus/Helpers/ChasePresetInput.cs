using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class ChasePresetInput
{
    // Vanilla reads the number pad directly, independently of Rewired bindings.
    // A weapon-camera press owns that shortcut, even when waiting for a launch.
    internal static bool Read(KeyCode key, bool keyDown, bool weaponCameraRequested) =>
        keyDown && !(weaponCameraRequested && key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9);
}
