using System.Reflection;
using ChaseCamPlus.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus.Patches;

public class CameraChaseStatePatches
{
    // Where we want to be, and where we currently are. Kept apart so the camera eases into the
    // orbit the same way the rest of the chase state eases, instead of snapping frame to frame.
    private static float _desiredPan;
    private static float _desiredTilt;
    private static float _pan;
    private static float _tilt;

    private static void ResetOrbit()
    {
        _desiredPan = 0f;
        _desiredTilt = 0f;
        _pan = 0f;
        _tilt = 0f;
    }

    /// <summary>Start every chase session from the default angle.</summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.EnterState))]
    static class EnterState
    {
        static void Postfix() => ResetOrbit();
    }

    /// <summary>
    /// Hiding the minimap widgets is a chase-only decision, so leaving chase has to put them back —
    /// otherwise the cockpit inherits a missing map from a setting that was never about it.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.LeaveState))]
    static class LeaveState
    {
        static void Postfix()
        {
            UpdateState.ShowMapWidgets(show: true);
            UpdateState.ApplyElementOverrides(inChase: false);
        }
    }

    /// <summary>
    /// Vanilla hides the flight HUD in chase view. <c>EnterState</c> clears <c>showHUD</c>, and
    /// <c>CheckHUD</c> short-circuits on it before ever reaching the branch that would turn the HUD
    /// on; the only thing that sets the flag back is <c>ToggleHUD</c>, reachable solely through a
    /// debug key gated on <c>GameState.Editor</c>. So in an ordinary match third person means no
    /// instruments and no virtual joystick indicator.
    ///
    /// Setting the flag before the method runs hands the decision back to the game's own logic
    /// rather than forcing the canvas on: the HUD appears in the positions it was written for
    /// (Back, Tail, wing roots, Belly) and stays off in the ones where an overlay would be nonsense.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.CheckHUD))]
    static class CheckHUD
    {
        static void Prefix(ref bool ___showHUD)
        {
            if (Plugin.Enabled.Value && Plugin.FlightHudInChase.Value)
                ___showHUD = true;
        }
    }

    /// <summary>
    /// Vanilla <c>CameraChaseState</c> never reads "Pan View" / "Tilt View" / "Free Look" — chase is
    /// the only camera state in the game with no free look at all. Rather than reproduce the state's
    /// positioning maths, this runs after it and rotates the finished camera around the aircraft, so
    /// it composes with the numpad view presets, the zoom, and the existing smoothing.
    /// </summary>
    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.UpdateState))]
    static class UpdateState
    {
        static void Postfix(CameraStateManager cam)
        {
            // Before the free look, and regardless of whether it is switched on at all.
            EnforceHudVisibility();

            if (!Plugin.Enabled.Value || !Plugin.FreeLook.Value)
            {
                ResetOrbit();
                return;
            }

            if (cam == null || cam.cameraPivot == null || cam.followingUnit == null)
                return;

            float dt = Time.unscaledDeltaTime;

            bool held = PatchHelper.FreeLookHeld()
                        && GameManager.flightControlsEnabled
                        && !DynamicMap.mapMaximized
                        && !RadialMenuMain.IsInUse();

            if (held)
            {
                // Same shape as the cockpit free look, so both views feel alike. Follows the game's
                // own invert-pitch preference, with a config switch on top for anyone who wants the
                // chase orbit to go the other way without disturbing the cockpit view.
                bool invertPitch = PlayerSettings.viewInvertPitch ^ Plugin.InvertFreeLookPitch.Value;
                float invert = invertPitch ? -1f : 1f;
                float sensitivity = PlayerSettings.viewSensitivity * Plugin.FreeLookSensitivity.Value;

                _desiredPan += GameManager.playerInput.GetAxis("Pan View") * 120f * sensitivity * dt;
                _desiredTilt += GameManager.playerInput.GetAxis("Tilt View") * 120f * sensitivity * dt * invert;

                float maxPitch = Mathf.Clamp(Plugin.FreeLookMaxPitch.Value, 0f, 89f);
                _desiredPan = Mathf.Clamp(_desiredPan, -180f, 180f);
                _desiredTilt = Mathf.Clamp(_desiredTilt, -maxPitch, maxPitch);
            }
            else if (Plugin.RecenterOnRelease.Value)
            {
                _desiredPan = 0f;
                _desiredTilt = 0f;
            }

            float t = Mathf.Min(5f * dt / Mathf.Max(PlayerSettings.viewSmoothing, 0.01f), 1f);
            _pan = Mathf.Lerp(_pan, _desiredPan, t);
            _tilt = Mathf.Lerp(_tilt, _desiredTilt, t);

            if (Mathf.Abs(_pan) < 0.01f && Mathf.Abs(_tilt) < 0.01f)
                return;

            Vector3 pivot = cam.cameraPivot.position;
            Vector3 offset = cam.transform.position - pivot;
            if (offset.sqrMagnitude < 0.0001f)
                return;

            Vector3 up = cam.followingUnit.transform.up;

            // The camera's own right, not a cross product of up and the offset. That cross points
            // to the aircraft's left whenever the camera sits behind it — which is every default
            // chase position — so the tilt came out mirrored; and it flipped sign again for the
            // Front preset, where the offset points the other way. Taking the axis from the camera
            // the game has already aimed at the aircraft keeps "mouse up raises the view" true from
            // every preset, and never degenerates the way the cross product did looking straight
            // down from Top or up from Belly.
            Vector3 right = cam.transform.right;

            // Tilt first in the vertical plane through the camera, then pan around the aircraft.
            Quaternion orbit = Quaternion.AngleAxis(_pan, up) * Quaternion.AngleAxis(_tilt, right);

            cam.transform.position = pivot + orbit * offset;
            cam.transform.rotation = orbit * cam.transform.rotation;

            KeepOutOfTerrain(cam, pivot);
        }

        /// <summary>
        /// Keeps the HUD in the state the config asks for, checked every frame instead of once on
        /// entry.
        ///
        /// Setting <c>showHUD</c> in <c>CheckHUD</c> is not enough on its own, because that method
        /// only runs when the chase state is entered, while plenty of other code turns the HUD off
        /// afterwards and turns it back on under a cockpit-only condition. Pausing runs
        /// <c>FlightHud.EnableCanvas(false)</c> unconditionally, and the resume path only restores
        /// it <c>if (currentState == cockpitState)</c>. Worse, <c>SwitchState</c> calls
        /// <c>DynamicMap.Minimize()</c> immediately *after* <c>EnterState</c>, and that method ends
        /// with <c>EnableCanvas(aircraft != null &amp;&amp; cameraMode == cockpit)</c> — so the map is
        /// switched off a moment after entering chase had switched it on.
        ///
        /// Rather than chase every one of those paths, and every new one a game update might add,
        /// this repairs the state whenever it finds it wrong. <c>EnableCanvas</c> drags a
        /// <c>RefreshSettings()</c> across five singletons behind it, so it is only called when
        /// something actually needs changing, never once per frame.
        /// </summary>
        private static void EnforceHudVisibility()
        {
            if (!Plugin.Enabled.Value || !Plugin.FlightHudInChase.Value)
                return;

            FlightHud hud = SceneSingleton<FlightHud>.i;
            bool hudActive = hud != null && FlightHudCanvasActive(hud);

            if (DynamicMap.mapMaximized)
            {
                // Opening the map full screen hides the flight HUD so it is not painted over the
                // map — but `Maximize()` only does that `if (currentState == cockpitState)`. In
                // chase that branch never runs, so the HUD stays up: a translucent grey overlay with
                // the green velocity vector sitting on top of the map. Do what the cockpit does.
                if (hudActive)
                    FlightHud.EnableCanvas(enable: false);

                // The player asked for the map, so put its widgets back whatever MapInChase says —
                // that flag is about the minimap in chase, not about a map opened deliberately.
                ShowMapWidgets(true);
                return;
            }

            if (hud != null && !hudActive)
                FlightHud.EnableCanvas(enable: true);

            ShowMapWidgets(Plugin.MapInChase.Value);
            ApplyElementOverrides(inChase: true);
        }

        /// <summary>
        /// Shows or hides the minimap and the chrome drawn around it.
        ///
        /// Switching off the <c>DynamicMap</c> object alone leaves the widget's furniture behind: a
        /// dark plate in the lower left with a small forward-reference marker on it, both of which
        /// live outside that object. The plate is the <c>Image</c> on the panel two levels up, and
        /// the marker is a sibling of the map under the anchor between them — so hiding the anchor
        /// takes the marker and the map together, and disabling just that one Image takes the plate.
        ///
        /// The panel itself has to stay active: it also carries the threat list, and switching it
        /// off would cost the player their missile warnings to tidy up an empty frame.
        ///
        /// Objects are reached by walking up from the map rather than by name, so this survives a
        /// renamed hierarchy as long as the shape of it holds.
        /// </summary>
        internal static void ShowMapWidgets(bool show)
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null)
                return;

            Transform anchor = map.transform.parent;
            Transform panel = anchor != null ? anchor.parent : null;

            if (anchor != null && anchor.gameObject.activeSelf != show)
                anchor.gameObject.SetActive(show);

            if (show && !map.gameObject.activeSelf)
                DynamicMap.EnableCanvas(enable: true);

            SetPlateVisible(panel, show);
        }

        /// <summary>
        /// Toggles the panel's own Image without touching its children. Matched on type name and
        /// handled as a <see cref="Behaviour"/> so this does not need a reference to the UI assembly
        /// for a single <c>enabled</c> flag.
        /// </summary>
        private static void SetPlateVisible(Transform panel, bool show)
        {
            if (panel == null)
                return;

            foreach (Component component in panel.GetComponents<Component>())
            {
                if (component is Behaviour behaviour
                    && component.GetType().Name == "Image"
                    && behaviour.enabled != show)
                {
                    behaviour.enabled = show;
                }
            }
        }

        /// <summary>
        /// Applies the per-element chase overrides, and clears them again when leaving chase so the
        /// cockpit never inherits a setting that was only ever about third person.
        ///
        /// Note what these two cannot do: the pitch ladder's rungs and its horizon line are one
        /// scrolling texture — the game moves a single <c>RawImage</c>'s uvRect with pitch — so
        /// hiding the ladder takes the horizon line with it. Separating them would mean replacing
        /// the texture, not toggling an object.
        /// </summary>
        internal static void ApplyElementOverrides(bool inChase)
        {
            FlightHud hud = SceneSingleton<FlightHud>.i;
            if (hud == null)
                return;

            bool showLadder = !(inChase && Plugin.HidePitchLadderInChase.Value);
            if (Field(ref _pitchLadderField, "pitchCompassCenter")?.GetValue(hud) is GameObject ladder
                && ladder.activeSelf != showLadder)
            {
                ladder.SetActive(showLadder);
            }

            // The weapon HUD states switch the waterline back on whenever the selected weapon
            // changes, so this is re-applied every frame rather than set once.
            bool showWaterline = !(inChase && Plugin.HideWaterlineInChase.Value);
            if (Field(ref _waterlineField, "waterline")?.GetValue(hud) is Behaviour waterline
                && waterline.enabled != showWaterline)
            {
                waterline.enabled = showWaterline;
            }
        }

        private static FieldInfo _pitchLadderField;
        private static FieldInfo _waterlineField;

        private static FieldInfo Field(ref FieldInfo cache, string name) =>
            cache ??= AccessTools.Field(typeof(FlightHud), name);

        // FlightHud.canvas is private, and there is no public way to ask whether the HUD is showing.
        private static FieldInfo _canvasField;

        private static bool FlightHudCanvasActive(FlightHud hud)
        {
            _canvasField ??= AccessTools.Field(typeof(FlightHud), "canvas");

            // Typed as Component rather than Canvas: only the GameObject is needed, and naming
            // Canvas would drag in a reference to UnityEngine.UIModule for nothing.
            if (_canvasField?.GetValue(hud) is Component canvas)
                return canvas.gameObject.activeSelf;

            // Cannot tell — assume it is fine rather than hammer EnableCanvas every frame.
            return true;
        }

        /// <summary>
        /// Orbiting can swing the camera through the ground. This repeats the collision pull-in the
        /// chase state does for its own placement, using the same cast and the same layer mask.
        /// </summary>
        private static void KeepOutOfTerrain(CameraStateManager cam, Vector3 pivot)
        {
            Vector3 toPivot = pivot - cam.transform.position;
            if (toPivot.sqrMagnitude < 0.0001f)
                return;

            if (!Physics.Linecast(pivot, pivot - toPivot * 2f, out RaycastHit hit, PhysicsLayers.StaticsMask))
                return;

            float facing = Mathf.Max(Vector3.Dot(toPivot.normalized, hit.normal), 0.1f);
            Vector3 pulled = hit.point + toPivot.normalized / facing;

            if ((pulled - pivot).sqrMagnitude < toPivot.sqrMagnitude)
                cam.transform.position = pulled;
        }
    }
}
