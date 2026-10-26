using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcadeCam.Helpers;

// The normal chase camera runs throughout. Only its rendered pose/FoV changes;
// the pilot, followed unit, camera state, flight axes and weapon simulation do not.
internal static class WeaponCamera
{
    private static readonly WeaponCameraState State = new();
    private static readonly WeaponCameraMotion Motion = new();
    private static readonly WeaponCameraFollow Follow = new();
    private static readonly WeaponImpactTarget ImpactTarget = new();
    private static readonly WeaponImpactMotion ImpactMotion = new();
    private static readonly FieldInfo MissileTarget = AccessTools.Field(typeof(Missile), "target");
    private sealed class RenderAnchor { internal Transform Node; internal GlobalPosition Position; internal Quaternion Rotation; }
    private static readonly RenderAnchor[] SkyAnchors = { new(), new() };
    private static readonly FieldInfo[] SkyFields = {
        AccessTools.Field(typeof(CameraStateManager), "sunObject"),
        AccessTools.Field(typeof(CameraStateManager), "moonObject")
    };
    private static Aircraft _owner;
    private static Missile _latest, _tracked;
    private static CameraStateManager _camera;
    private static bool _initialized, _held, _ended, _impactPending, _prepared, _rendering;
    private static bool _hasOverride, _startPending;
    private static int _renderDepth;
    private static Transform _baseParent;
    private static Vector3 _baseLocalPosition;
    private static Quaternion _baseLocalRotation;
    private static WeaponViewPhase _posePhase;
    private static GlobalPosition _position, _impact, _returnPosition, _basePosition;
    private static Vector3 _right = Vector3.right, _impactOffset;
    private static Quaternion _rotation, _returnRotation, _baseRotation;
    private static float _fov, _returnFov, _baseFov, _baseNearClip;
    private static float _impactDuration, _impactFollowStart;
    private static bool _impactFollowing;

    internal static bool IsDisplaying => State.Displaying;

    internal static void Initialize(Harmony harmony)
    {
        if (_initialized) return;
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
        RenderPipelineManager.endFrameRendering += EndFrame;
        Canvas.willRenderCanvases += BeforeCanvasRender;
        _initialized = true;
        int hooks = 0;
        // Observe the executed RPC body on both host and client. Do not intercept
        // or replace the warhead, damage, detonation timing or network messages.
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Missile)))
        {
            if (!method.Name.StartsWith("UserCode_RpcDetonate_", StringComparison.Ordinal)) continue;
            var args = method.GetParameters();
            if (args.Length != 7 || args[0].ParameterType != typeof(Unit)
                || args[1].ParameterType != typeof(bool) || args[2].ParameterType != typeof(Vector3)
                || args[3].ParameterType != typeof(bool) || args[4].ParameterType != typeof(bool)
                || args[5].ParameterType != typeof(bool) || args[6].ParameterType != typeof(Vector3)) continue;
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(WeaponCamera), nameof(ObserveDetonation)));
                hooks++;
            }
            catch (Exception error)
            {
                Plugin.Logger.LogWarning("Weapon camera: impact observer could not be installed: " + error.Message);
            }
        }
        if (hooks == 0) Plugin.Logger.LogWarning("Weapon camera: native detonation callback was not found. Tracking and return remain available; impact orbit is unavailable.");
    }

    private static void ObserveDetonation(Missile __instance, Unit __0, bool __1, Vector3 __2, bool __3)
    {
        if (__instance != _tracked || State.Phase != WeaponViewPhase.Following || _impactPending || !__3) return;
        // Match the game's RPC position conversion, including a moving target.
        Vector3 point = __0 != null ? __0.transform.TransformPoint(__2) : __2 + Datum.origin.position;
        _impact = point.ToGlobalPosition();
        Unit target = __0 != null ? __0 : MissileTarget?.GetValue(__instance) as Unit;
        ImpactTarget.Detonate(target == _owner ? null : target, point);
        _impactPending = true;
    }

    private static void BindOwner(Aircraft aircraft)
    {
        if (_owner == aircraft) return;
        Cancel();
        if (_owner != null)
        {
            _owner.onRegisterMissile -= Registered;
            _owner.onDeregisterMissile -= Deregistered;
        }
        _owner = aircraft; _latest = null;
        if (_owner != null)
        {
            _owner.onRegisterMissile += Registered;
            _owner.onDeregisterMissile += Deregistered;
        }
    }

    private static void Registered(Missile missile)
    {
        if (missile != null && !missile.disabled && missile.owner == _owner) _latest = missile;
    }

    private static void Deregistered(Missile missile)
    {
        if (missile == _tracked) _ended = true;
        if (missile == _latest) _latest = null;
    }

    internal static void Poll()
    {
        RestoreVisual(); // Fallback if a render was interrupted.
        GameManager.GetLocalAircraft(out Aircraft aircraft);
        if (aircraft != null && (aircraft.HasEjected() || aircraft.disabled)) aircraft = null;
        BindOwner(aircraft);
        var player = GameManager.playerInput;
        int action = Plugin.WeaponCameraActionId;
        _held = player != null && action >= 0 && player.GetButton(action);
        bool down = player != null && action >= 0 && player.GetButtonDown(action);
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        bool allowed = Plugin.IsActive && Plugin.WeaponCameraEnabled.Value && ValidContext(cam);
        int? latest = _latest != null && !_latest.disabled && _latest.gameObject.activeInHierarchy
            ? _latest.GetInstanceID() : (int?)null;
        if (State.Input(_held, down, Plugin.WeaponCameraToggle.Value, allowed, latest))
        {
            _tracked = _latest; _camera = cam;
            // Initialize after native chase LateUpdate, never from an Update pose
            // which may still contain the preceding frame's free-look overlays.
            _startPending = true;
            _ended = false; _impactPending = false; _posePhase = WeaponViewPhase.Off;
            Motion.Reset(); Follow.Reset();
            ImpactTarget.Reset(); _impactFollowing = false;
        }
        if (!State.Displaying) { _prepared = false; _startPending = false; RestoreHud(); }
        if (down && allowed && !latest.HasValue && !State.Displaying)
            Plugin.Logger.LogInfo("Weapon camera: no live player missile/bomb; chase view unchanged.");
    }

    private static bool ValidContext(CameraStateManager cam) => cam != null && _owner != null
        && !_owner.disabled && !_owner.HasEjected() && cam.followingUnit == _owner
        && cam.mainCamera != null && ChaseInput.IsAllowed(cam);

    internal static void Prepare(CameraStateManager cam)
    {
        _prepared = false;
        if (!State.Displaying) { _tracked = null; ImpactTarget.Reset(); RestoreHud(); return; }
        if (!Plugin.WeaponCameraEnabled.Value || cam != _camera || !ValidContext(cam)) { Cancel(); return; }
        if (_startPending)
        {
            _position = cam.transform.GlobalPosition(); _rotation = cam.transform.rotation;
            _fov = cam.mainCamera.fieldOfView; _right = cam.transform.right;
            _startPending = false;
        }
        float dt = Mathf.Max(0f, Time.deltaTime);
        float impactSeconds = Plugin.WeaponCameraCinematic.Value ? Plugin.WeaponCameraImpactSeconds.Value : 0f;
        if (State.Phase == WeaponViewPhase.Following && !_impactPending && _tracked != null && !_tracked.disabled)
        {
            Unit target = MissileTarget?.GetValue(_tracked) as Unit;
            ImpactTarget.Watch(target == _owner ? null : target);
        }
        if (_impactPending && State.Phase == WeaponViewPhase.Following)
        {
            _impactOffset = _position.ToLocalPosition() - _impact.ToLocalPosition();
            if (_impactOffset.sqrMagnitude < 64f) _impactOffset = -(_rotation * Vector3.forward) * 12f;
            State.Detonated(impactSeconds);
            _impactDuration = impactSeconds; _impactFollowing = false;
            _impactPending = false;
        }
        else if (State.Phase == WeaponViewPhase.Following
            && (_ended || _tracked == null || _tracked.disabled || !_tracked.gameObject.activeInHierarchy))
        {
            // Disabled replication may arrive just before the native detonation
            // RPC. Briefly retain the last pose to receive that confirmation.
            State.Lost(dt, impactSeconds > 0f ? 0.08f : 0f);
            if (State.Phase == WeaponViewPhase.Following)
            {
                _posePhase = State.Phase; _prepared = true;
                UpdateHudVisibility();
                return;
            }
        }
        Vector3 hitPoint = Vector3.zero;
        float hitRadius = 0f;
        if (State.Phase == WeaponViewPhase.Impact)
        {
            bool canFollow = impactSeconds > 0f && Plugin.WeaponCameraFollowHitTarget.Value
                && Plugin.WeaponCameraWreckageSeconds.Value > 0f;
            if (_impactFollowing && (!canFollow || !ImpactTarget.TryPoint(out hitPoint, out hitRadius)))
                State.Return();
            else if (!_impactFollowing && canFollow && ImpactTarget.TryPoint(out hitPoint, out hitRadius))
            {
                _impactFollowing = true; _impactFollowStart = State.PhaseTime;
                _impactDuration = State.PhaseTime + Plugin.WeaponCameraWreckageSeconds.Value;
                ImpactMotion.Begin(_position.ToLocalPosition() - hitPoint);
                Plugin.Logger.LogInfo("Weapon camera: following damaged target or its surviving debris.");
            }
        }
        State.Advance(dt, _impactFollowing ? _impactDuration : impactSeconds, Plugin.WeaponCameraReturnSeconds.Value);
        if (State.Phase != WeaponViewPhase.Following && State.Phase != WeaponViewPhase.Impact) ImpactTarget.Reset();
        if (!State.Displaying) { _tracked = null; _posePhase = WeaponViewPhase.Off; RestoreHud(); return; }

        if (State.Phase == WeaponViewPhase.Following)
        {
            Vector3 point = _tracked.transform.position;
            Vector3 velocity = _tracked.rb != null ? _tracked.rb.velocity : Vector3.zero;
            Vector3 forward = velocity.sqrMagnitude > 25f ? velocity.normalized : _tracked.transform.forward;
            _right = WeaponCameraMotion.Right(forward, _right);
            Motion.Advance(forward, _right, velocity.magnitude, _tracked.maxRadius,
                Plugin.WeaponCameraDistanceScale.Value, Plugin.WeaponCameraCinematic.Value, dt);
            Vector3 wantedOffset = -forward * Motion.Distance + _right * (Motion.Distance * Motion.Side)
                                   + Vector3.up * (Motion.Distance * Motion.Height);
            bool acquiring = !Follow.Initialized;
            float blend = acquiring ? 1f : 1f - Mathf.Exp(-dt / 0.18f);
            Vector3 position = point + Follow.Advance(wantedOffset, dt);
            position = ClearTerrain(point, position);
            Vector3 aim = point + forward * (Motion.Distance * 0.12f);
            Quaternion rotation = LookAt(aim - position, _rotation);
            _position = position.ToGlobalPosition();
            _rotation = Quaternion.Slerp(_rotation, rotation, blend);
            _fov = Mathf.Lerp(_fov, Plugin.WeaponCameraFieldOfView.Value, blend);
            _tracked.displayDetail = Mathf.Max(_tracked.displayDetail, 1000f);
        }
        else if (State.Phase == WeaponViewPhase.Impact)
        {
            Vector3 point = _impactFollowing ? hitPoint : _impact.ToLocalPosition();
            Vector3 wanted = _impactFollowing
                ? ImpactMotion.Advance(point, hitRadius, State.PhaseTime - _impactFollowStart, dt)
                : point + Quaternion.AngleAxis(State.PhaseTime * 14f, Vector3.up) * _impactOffset;
            Vector3 position = ClearTerrain(point + Vector3.up, wanted);
            _position = position.ToGlobalPosition();
            _rotation = LookAt(point - position, _rotation);
        }
        else if (State.Phase == WeaponViewPhase.Returning)
        {
            if (_posePhase != WeaponViewPhase.Returning)
            {
                _returnPosition = _position; _returnRotation = _rotation; _returnFov = _fov;
            }
            float blend = State.ReturnBlend(Plugin.WeaponCameraReturnSeconds.Value);
            // The destination continues to follow the live aircraft while returning.
            _position = Vector3.Lerp(_returnPosition.ToLocalPosition(), cam.transform.position, blend).ToGlobalPosition();
            _rotation = Quaternion.Slerp(_returnRotation, cam.transform.rotation, blend);
            _fov = Mathf.Lerp(_returnFov, cam.mainCamera.fieldOfView, blend);
        }
        _posePhase = State.Phase;
        _prepared = true;
        UpdateHudVisibility();
    }

    private static Quaternion LookAt(Vector3 direction, Quaternion previous)
    {
        if (direction.sqrMagnitude < 0.0001f) return previous;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, direction.normalized);
        if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(previous * Vector3.up, direction.normalized);
        if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(Vector3.forward, direction.normalized);
        return Quaternion.LookRotation(direction, up);
    }

    private static Vector3 ClearTerrain(Vector3 point, Vector3 position) =>
        Physics.Linecast(point, position, out RaycastHit hit, PhysicsLayers.StaticsMask)
            ? hit.point + hit.normal * 0.7f : position;

    // Keep the origin at the actual rendered view, rather than shifting it back
    // to the aircraft and then out to a distant missile twice on every frame.
    internal static bool ManagesOrigin(CameraStateManager cam) => !_rendering && _prepared
        && State.Displaying && cam == _camera && ValidContext(cam);

    private static void BeginRender(ScriptableRenderContext context, Camera camera)
    {
        if (!_prepared || !State.Displaying || _camera == null || camera != _camera.mainCamera || !ValidContext(_camera)) return;
        // A nested render must not replace the original aircraft snapshot with
        // the temporary weapon pose. Only the matching outer end restores it.
        if (_hasOverride) { _renderDepth++; return; }
        _renderDepth = 1;
        _baseParent = _camera.transform.parent;
        _baseLocalPosition = _camera.transform.localPosition;
        _baseLocalRotation = _camera.transform.localRotation;
        _basePosition = _camera.transform.GlobalPosition(); _baseRotation = _camera.transform.rotation;
        _baseFov = camera.fieldOfView; _baseNearClip = camera.nearClipPlane; _hasOverride = true;
        Vector3 delta = _position.ToLocalPosition() - _camera.transform.position;
        for (int i = 0; i < SkyAnchors.Length; i++)
        {
            SkyAnchors[i].Node = (SkyFields[i]?.GetValue(_camera) as Renderer)?.transform;
            if (SkyAnchors[i].Node == null) continue;
            SkyAnchors[i].Position = SkyAnchors[i].Node.GlobalPosition();
            SkyAnchors[i].Rotation = SkyAnchors[i].Node.rotation;
        }
        Vector3 shake = State.Phase == WeaponViewPhase.Impact
            ? WeaponImpactMotion.Shake(State.PhaseTime, Plugin.WeaponCameraImpactShake.Value) : Vector3.zero;
        _camera.transform.SetPositionAndRotation(_position.ToLocalPosition(), _rotation * Quaternion.Euler(shake));
        for (int i = 0; i < SkyAnchors.Length; i++)
        {
            if (SkyAnchors[i].Node == null) continue;
            SkyAnchors[i].Node.SetPositionAndRotation(SkyAnchors[i].Position.ToLocalPosition() + delta, SkyAnchors[i].Rotation);
        }
        camera.fieldOfView = _fov;
        camera.nearClipPlane = Mathf.Min(_baseNearClip, 0.1f);
        try
        {
            _rendering = true;
            _camera.CheckOriginShift();
        }
        catch (Exception error)
        {
            Cancel();
            Plugin.Logger.LogError("Weapon camera render was cancelled: " + error.Message);
            return;
        }
        finally { _rendering = false; }
        if (Plugin.WeaponCameraHideHud.Value) WeaponCameraHud.Reapply();
    }

    private static void EndRender(ScriptableRenderContext context, Camera camera)
    {
        if (_hasOverride && _camera != null && camera == _camera.mainCamera && --_renderDepth <= 0)
            RestoreVisual();
    }

    private static void EndFrame(ScriptableRenderContext context, Camera[] cameras)
    {
        // Nested render contexts can end while the outer camera is still live.
        // Interrupted callbacks are also recovered before the next camera update.
        // Overlay HUD drawing may still be pending. Its visibility belongs to
        // the weapon session, not this camera/context's rendering lifetime.
        if (_renderDepth == 0) RestoreVisual();
    }

    private static void RestoreVisual()
    {
        if (!_hasOverride) return;
        _hasOverride = false; _renderDepth = 0;
        if (_camera != null && _camera.mainCamera != null)
        {
            // The chase camera belongs to an aircraft/pivot hierarchy. Restore
            // its own local pose so parent motion cannot become a saved pan/roll.
            if (_camera.transform.parent == _baseParent)
            {
                _camera.transform.localPosition = _baseLocalPosition;
                _camera.transform.localRotation = _baseLocalRotation;
            }
            else
                _camera.transform.SetPositionAndRotation(_basePosition.ToLocalPosition(), _baseRotation);
            _camera.mainCamera.fieldOfView = _baseFov;
            _camera.mainCamera.nearClipPlane = _baseNearClip;
        }
        for (int i = 0; i < SkyAnchors.Length; i++)
        {
            if (SkyAnchors[i].Node != null)
                SkyAnchors[i].Node.SetPositionAndRotation(SkyAnchors[i].Position.ToLocalPosition(), SkyAnchors[i].Rotation);
            SkyAnchors[i].Node = null;
        }
    }
    private static void UpdateHudVisibility()
    {
        if (_prepared && State.Displaying && Plugin.WeaponCameraHideHud.Value) WeaponCameraHud.Hide();
        else RestoreHud();
    }

    private static void BeforeCanvasRender()
    {
        if (_prepared && State.Displaying && Plugin.WeaponCameraHideHud.Value && ValidContext(_camera))
            WeaponCameraHud.Reapply();
        else RestoreHud();
    }

    private static void RestoreHud() => WeaponCameraHud.Restore();

    internal static void BeforeCameraUpdate(CameraStateManager cam)
    {
        // Run at the actual native update boundary too: Plugin.Update ordering
        // cannot guarantee recovery before another mod updates the camera.
        if (cam != _camera) return;
        RestoreVisual();
    }

    internal static void CancelFor(CameraStateManager cam)
    {
        if (cam == _camera) Cancel();
    }

    internal static void Cancel()
    {
        RestoreVisual(); RestoreHud();
        State.Cancel(_held); _prepared = false; _tracked = null;
        _impactPending = false; _ended = false; _startPending = false; _posePhase = WeaponViewPhase.Off;
        Follow.Reset();
        ImpactTarget.Reset(); _impactFollowing = false;
    }

    internal static void Dispose()
    {
        Cancel(); BindOwner(null);
        if (!_initialized) return;
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        RenderPipelineManager.endFrameRendering -= EndFrame;
        Canvas.willRenderCanvases -= BeforeCanvasRender;
        _initialized = false;
    }
}
