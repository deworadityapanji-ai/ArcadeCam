using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace ArcadeCam.Helpers;

// Display the existing camera output. Never clone a sensor, change its target,
// render through terrain, or keep a native camera running after it has stopped.
internal static class TargetingScreenOverlay
{
    private sealed class NativeDisplay
    {
        internal TacScreen Owner;
        internal RawImage Image;
    }
    private static readonly Dictionary<Aircraft, NativeDisplay> Displays = new();
    private static readonly FieldInfo DisplayField = AccessTools.Field(typeof(TacScreen), "targetCamDisplay");
    private static readonly FieldInfo CameraField = AccessTools.Field(typeof(TargetCam), "cam");
    private static readonly FieldInfo ModeField = AccessTools.Field(typeof(TargetCam), "currentMode");
    private static readonly FieldInfo CanvasField = AccessTools.Field(typeof(FlightHud), "canvas");

    private static Camera _sensorCamera;
    private static CameraStateManager _view;
    private static RenderTexture _texture;
    private static Rect _uv;
    private static readonly TargetingFeedReadiness Feed = new();
    private static bool _listening;
    private static bool _warned;
    internal static void Register(TacScreen screen, Aircraft aircraft)
    {
        if (screen == null || aircraft == null) return;
        if (DisplayField?.GetValue(screen) is GameObject display)
        {
            RawImage image = display.GetComponentInChildren<RawImage>(true);
            if (image != null) Displays[aircraft] = new NativeDisplay { Owner = screen, Image = image };
        }
    }

    internal static void Unregister(TacScreen screen, Aircraft aircraft)
    {
        if (!ReferenceEquals(aircraft, null) && Displays.TryGetValue(aircraft, out NativeDisplay display)
            && ReferenceEquals(display.Owner, screen)) Displays.Remove(aircraft);
    }

    internal static void Hide()
    {
        Feed.Reset();
        _sensorCamera = null;
        _view = null;
        _texture = null;
    }

    internal static void Dispose()
    {
        Hide();
        if (_listening) RenderPipelineManager.endCameraRendering -= OnCameraRendered;
        _listening = false;
        Displays.Clear();
    }
    internal static void Tick(CameraStateManager cam)
    {
        if (WeaponCamera.IsDisplaying || !Plugin.IsActive || !Plugin.TargetingScreenEnabled.Value || cam == null
            || cam.currentState != cam.chaseState || !Plugin.FlightHud.Value || !ChaseInput.IsAllowed(cam))
        { Hide(); return; }
        if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null
            || aircraft != cam.followingUnit || aircraft.HasEjected() || aircraft.disabled
            || aircraft.targetCam == null || aircraft.weaponManager == null)
        { Hide(); return; }
        FlightHud hud = SceneSingleton<FlightHud>.i;
        Canvas canvas = hud == null ? null : CanvasField?.GetValue(hud) as Canvas;
        if (canvas == null || !canvas.isActiveAndEnabled || !canvas.gameObject.activeInHierarchy)
        { Hide(); return; }
        TargetCam sensor = aircraft.targetCam;
        Camera camera = CameraField?.GetValue(sensor) as Camera;
        bool landing = ModeField?.GetValue(sensor) is TargetCam.CamMode mode && mode == TargetCam.CamMode.landingMode;
        bool hasTarget = aircraft.weaponManager.GetTargetList()?.Count > 0;
        if (camera == null || !camera.isActiveAndEnabled || landing || !hasTarget)
        { Hide(); return; }

        // Never select an arbitrary cockpit/renderer texture: it can be the
        // combined tactical/radar display rather than this sensor's output.
        RenderTexture texture = camera.targetTexture;
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        if (Displays.TryGetValue(aircraft, out NativeDisplay display) && display.Image != null
            && display.Image.texture == texture)
            uv = display.Image.uvRect;
        bool ready = texture != null && texture.IsCreated();
        var state = new TargetingScreenState(true, true, true, true, hasTarget,
            camera.isActiveAndEnabled, ready, landing, true);
        if (!state.Visible)
        {
            Hide();
            if (texture == null && !_warned)
            {
                _warned = true;
                Plugin.Logger.LogWarning("Targeting screen: native camera is running but its output texture is unavailable. The overlay will wait for a valid feed.");
            }
            return;
        }
        _sensorCamera = camera;
        if (!_listening)
        {
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            _listening = true;
        }
        Feed.Observe(camera.GetInstanceID(), texture.GetInstanceID(), Time.frameCount);
        _view = cam;
        _texture = texture;
        _uv = uv;
    }

    // Draw after camera rendering without creating/rebuilding any HUD canvas.
    // The panel has no controls and never consumes controller/mouse events.
    internal static void Draw()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint
            || _view == null || !Plugin.IsActive || !Plugin.TargetingScreenEnabled.Value
            || !Plugin.FlightHud.Value || !ChaseInput.IsAllowed(_view) || Time.timeScale <= 0f
            || _view.mainCamera == null || _sensorCamera == null || !_sensorCamera.isActiveAndEnabled
            || _texture == null || !_texture.IsCreated() || _sensorCamera.targetTexture != _texture
            || !Feed.CanShow(Time.frameCount)) return;
        // Recheck the target at draw time; clearing it later in the frame must
        // not expose the last picture for another frame.
        if (!GameManager.GetLocalAircraft(out Aircraft aircraft) || aircraft == null
            || aircraft != _view.followingUnit || aircraft.HasEjected() || aircraft.disabled
            || aircraft.weaponManager == null || !(aircraft.weaponManager.GetTargetList()?.Count > 0)
            || aircraft.targetCam == null
            || (ModeField?.GetValue(aircraft.targetCam) is TargetCam.CamMode mode && mode == TargetCam.CamMode.landingMode))
            return;
        Rect viewport = _view.mainCamera.pixelRect;
        if (viewport.width <= 0f || viewport.height <= 0f) return;
        float aspect = _texture.width * Mathf.Abs(_uv.width)
                       / Mathf.Max(_texture.height * Mathf.Abs(_uv.height), 1f);
        TargetingScreenRect frame = TargetingScreenLayout.Calculate(viewport.width, viewport.height,
            Plugin.TargetingScreenXPercent.Value, Plugin.TargetingScreenYPercent.Value,
            Plugin.TargetingScreenWidthPercent.Value, aspect);
        TargetingScreenRect fitted = TargetingScreenLayout.FitImage(frame.Width, frame.Height, 2f, aspect);
        float left = viewport.x + frame.Left;
        float top = Screen.height - viewport.yMax + frame.Top;
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color;
        int depth = GUI.depth;
        bool guiEnabled = GUI.enabled;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = 0;
            GUI.enabled = true;
            GUI.color = new Color(0.15f, 0.24f, 0.25f, 0.95f);
            GUI.DrawTexture(new Rect(left, top, frame.Width, frame.Height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(left + fitted.Left, top + fitted.Top,
                fitted.Width, fitted.Height), _texture, _uv, false);
        }
        finally
        {
            GUI.matrix = matrix;
            GUI.color = color;
            GUI.depth = depth;
            GUI.enabled = guiEnabled;
        }
    }
    private static void OnCameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null || camera != _sensorCamera || camera.targetTexture == null) return;
        Feed.Rendered(camera.GetInstanceID(), camera.targetTexture.GetInstanceID(), Time.frameCount);
    }

}
