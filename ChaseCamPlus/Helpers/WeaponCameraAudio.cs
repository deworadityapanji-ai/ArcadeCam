using UnityEngine;

namespace ArcadeCam.Helpers;

// Audio needs a viewpoint for the whole frame; the flight camera's temporary
// rendered pose must still be restored for aircraft input and simulation.
internal static class WeaponCameraAudio
{
    private static readonly VisibilityOverride<AudioListener> Listeners = new(
        node => node != null, node => node.enabled, (node, visible) => node.enabled = visible);
    private static readonly VisibilityOverride<AudioSource> CameraSounds = new(
        node => node != null, node => !node.mute, (node, audible) => node.mute = !audible);
    private static AudioListener _original, _proxy;
    private static GameObject _object;
    private static GlobalPosition _position, _previousPosition;
    private static Vector3 _velocity;
    private static float _previousTime;
    private static bool _ready, _warned;

    internal static bool Active => _ready && _proxy != null && _proxy.isActiveAndEnabled;

    internal static void Tick(CameraStateManager cam)
    {
        if (!Plugin.WeaponCameraFollowAudio.Value || !WeaponCamera.TryGetAudioPose(cam, out var position, out var rotation))
        { Restore(); return; }
        if (_original == null || _proxy == null)
        {
            Restore();
            AudioListener selected = null;
            foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>())
            {
                if (listener == _proxy || !listener.isActiveAndEnabled) continue;
                if (selected != null) { Warn(); return; }
                selected = listener;
            }
            if (selected == null) { Warn(); return; }
            _original = selected;
            if (_object == null)
            {
                _object = new GameObject("ArcadeCam Weapon Audio");
                _object.SetActive(false);
                _proxy = _object.AddComponent<AudioListener>();
                _proxy.enabled = false;
            }
            _object.transform.SetParent(Datum.origin, false);
            _proxy.velocityUpdateMode = _original.velocityUpdateMode;
            Listeners.Hide(_original);
            _object.transform.SetPositionAndRotation(position.ToLocalPosition(), rotation);
            _proxy.enabled = true;
            _object.SetActive(true);
            _ready = false;
        }
        float now = Time.unscaledTime;
        float dt = now - _previousTime;
        _velocity = _ready && dt > 0.0001f && dt < 0.25f ? (position - _previousPosition) / dt : Vector3.zero;
        _position = _previousPosition = position;
        _previousTime = now;
        _object.transform.SetPositionAndRotation(position.ToLocalPosition(), rotation);
        Listeners.Reapply();
        CameraSounds.Hide(cam.cockpitRattle);
        CameraSounds.Hide(cam.windNoiseExternal);
        _ready = true;
    }

    internal static Vector3 ListenerPosition(Transform source)
    {
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        return Active && cam != null && source == cam.transform ? _position.ToLocalPosition() : source.position;
    }

    internal static Vector3 ListenerVelocity(CameraStateManager cam) => Active ? _velocity : cam.cameraVelocity;

    internal static void UseListenerPosition(ref GlobalPosition cameraPos)
    {
        if (Active) cameraPos = _position;
    }

    internal static void Restore()
    {
        if (_proxy != null) _proxy.enabled = false;
        if (_object != null) _object.SetActive(false);
        Listeners.Restore(); CameraSounds.Restore();
        _original = null; _ready = false; _velocity = Vector3.zero;
    }

    internal static void Dispose()
    {
        Restore();
        if (_object != null) Object.Destroy(_object);
        _object = null; _proxy = null;
    }

    private static void Warn()
    {
        if (_warned) return;
        _warned = true;
        Plugin.Logger.LogWarning("Weapon audio: expected one active audio listener; keeping native audio until the listener context is valid.");
    }
}
