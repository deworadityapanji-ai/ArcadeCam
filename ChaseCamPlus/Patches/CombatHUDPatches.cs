using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus.Patches;

public class CombatHUDPatches
{
    /// <summary>
    /// <c>CombatHUD.DisplayHit</c> runs on every round the local aircraft lands — <c>BulletSim</c>
    /// calls it with no regard for the camera. The cockpit check lives inside the method, and it
    /// wraps <em>both</em> the on-screen marker and the confirmation sound:
    ///
    /// <code>
    /// if (PlayerSettings.showHitMarkers &amp;&amp; cam.currentState == cam.cockpitState &amp;&amp; (...))
    /// {
    ///     ... place marker ...
    ///     SoundManager.PlayInterfaceOneShot(GameAssets.i.hitMarkerSound);
    /// }
    /// </code>
    ///
    /// So outside the cockpit there is no feedback at all until the target visibly comes apart.
    /// This prefix adds back only the audio, leaving the marker cockpit-only: placing markers from a
    /// chase camera is a separate question about screen-space projection, and the ask here was the
    /// sound. Running as a prefix that bails out in cockpit view means vanilla still owns that case,
    /// so the sound can never double up.
    /// </summary>
    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DisplayHit))]
    static class DisplayHit
    {
        private static float _lastSound;

        static void Prefix(Unit hitUnit)
        {
            if (!Plugin.Enabled.Value || !Plugin.HitSoundOutsideCockpit.Value)
                return;

            // Respect the game's own toggle: no markers means no hit feedback, audio included.
            if (!PlayerSettings.showHitMarkers)
                return;

            if (hitUnit == null)
                return;

            CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
            if (cam == null || cam.currentState == cam.cockpitState)
                return;

            // Same target filter vanilla applies before it counts a hit worth reporting.
            if (!(hitUnit is GroundVehicle || hitUnit is Aircraft || hitUnit.maxRadius <= 8f))
                return;

            // Vanilla throttles the sound to one per 0.05s so a burst doesn't stack into noise.
            // Tracked separately from the game's own timer, which stays untouched.
            //
            // Measured on unscaledTime rather than the timeSinceLevelLoad vanilla uses. Vanilla can
            // afford that clock because its timestamp lives on CombatHUD, a scene singleton that
            // dies with the scene; this one is static and outlives it. Pair a static timestamp with
            // a clock that resets to zero on scene load and the comparison goes permanently
            // negative after the first mission — silent until the new scene's clock climbs back past
            // whatever the old one reached. unscaledTime only ever moves forward.
            if (Time.unscaledTime - _lastSound < 0.05f)
                return;

            _lastSound = Time.unscaledTime;
            SoundManager.PlayInterfaceOneShot(GameAssets.i.hitMarkerSound);
        }
    }
}
