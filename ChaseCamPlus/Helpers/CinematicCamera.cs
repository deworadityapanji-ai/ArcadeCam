using UnityEngine;

namespace ChaseCamPlus.Helpers;

/// <summary>
/// A cinematic chase camera, where the aircraft is allowed to rotate and drift inside the frame
/// instead of the world rotating around it.
///
/// Three things separate it from the vanilla chase state, all visible frame by frame in reference
/// footage:
///
/// <list type="number">
/// <item>The horizon never tilts. Through a full roll the aircraft turns over and the world stays
/// level, so roll is not attenuated — it is simply not followed at all.</item>
/// <item>The boom is anchored to the <em>flight path</em>, not to the nose. During a hard pull the
/// aircraft sits visibly nose-high in the frame for seconds at a time, which only happens if the
/// camera is lined up with where the aircraft is going rather than where it is pointing.</item>
/// <item>That anchor is heavily damped. In a loop the camera does eventually come all the way over
/// the top and point down, so nothing is clamped — it is just late, and the lateness is the whole
/// effect.</item>
/// <item>The camera flies a course of its own, so the aircraft wanders across the frame rather than
/// only turning in place. See <see cref="UpdateOffset"/>, which is where that has to be earned back
/// after being lost to the floating origin.</item>
/// </list>
///
/// Vanilla cannot approach any of this by tuning, because <c>CameraChaseState</c> builds its offset
/// in the aircraft's own axes and takes the pivot's up vector from <c>followingUnit.transform.up</c>.
/// The rig is welded to the airframe: its two <c>Lerp</c>s are smoothing on a rigid arm, not lag on a
/// free one. Turning View Smoothing up delays the arm without ever letting it trail, and since the
/// camera looks <em>along</em> the pivot rather than <em>at</em> the aircraft, the aircraft leaves the
/// frame instead of rotating within it.
///
/// Every piece of state here is relative to the aircraft — a direction, an offset, a relative
/// velocity, a rotation. None of it is a world position, because the game runs a
/// <see cref="FloatingOrigin"/> that shifts the world out from under the camera; a cached absolute
/// position would survive that shift as a wrong one.
/// </summary>
internal static class CinematicCamera
{
    /// <summary>Hand-over time to and from the vanilla placement, in seconds.</summary>
    private const float TransitionTime = 0.35f;

    /// <summary>
    /// Speed band over which the velocity vector takes over from the nose as the anchor. Below the
    /// lower bound the rigidbody is barely moving and its velocity direction is noise; blending
    /// rather than switching keeps the camera from snapping as an aircraft rolls out on a runway.
    /// </summary>
    private const float SlowSpeed = 10f;

    private const float FlyingSpeed = 40f;

    /// <summary>
    /// How long the boom takes to give back the length a hard turn stretched out of it, in seconds.
    /// Also smooths the acceleration that drives it, which is a difference of sampled velocities and
    /// therefore noisy.
    /// </summary>
    private const float StretchLag = 0.3f;

    /// <summary>
    /// How long the speed feeding the pullback takes to follow the real one, in seconds.
    ///
    /// Deliberately long. The pullback is meant to say "this aircraft is travelling fast", which is a
    /// property of the last few seconds, not of this frame. Reading the instantaneous speed made the
    /// boom pump in and out through every manoeuvre — and worse, drew the camera *in* during a
    /// decelerating pull, which is precisely backwards from what the reference does.
    /// </summary>
    private const float CruiseSpeedLag = 3f;

    /// <summary>How long the mode's field of view takes to follow the FoV axis, in seconds.</summary>
    private const float FovLag = 0.15f;

    private static bool _on;
    private static float _blend;
    private static bool _settled;

    private static Vector3 _anchor;
    private static Vector3 _offset;
    private static Vector3 _offsetVelocity;
    private static Vector3 _lastVelocity;
    private static Vector3 _lastRight = Vector3.right;
    private static Quaternion _rotation = Quaternion.identity;
    private static Vector3 _course;
    private static float _stretch;
    private static float _cruiseSpeed;
    private static float _fov;

    /// <summary>
    /// Whether the mode is switched on. Says nothing about which camera is live — callers that care
    /// (the turret, the HUD) are already inside a chase-only path.
    /// </summary>
    internal static bool Active => _on && Plugin.Enabled.Value && Plugin.CinematicChaseCam.Value;

    /// <summary>
    /// Drops the smoothing state so the next frame places the camera outright instead of easing in
    /// from wherever the last chase session left it. Called when the chase state is entered and
    /// whenever the mode is toggled.
    /// </summary>
    internal static void Reset()
    {
        _settled = false;
        _blend = 0f;
        _offsetVelocity = Vector3.zero;
        _stretch = 0f;
    }

    /// <summary>Reads the toggle binding. Mirrors the guards <see cref="CameraToggle"/> applies.</summary>
    internal static void Poll()
    {
        if (!Plugin.Enabled.Value || !Plugin.CinematicChaseCam.Value)
            return;

        if (Plugin.CinematicActionId < 0)
            return;

        var player = GameManager.playerInput;
        if (player == null || !player.GetButtonDown(Plugin.CinematicActionId))
            return;

        if (!GameManager.flightControlsEnabled)
            return;

        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null)
            return;

        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        if (picker != null && picker.isOpen)
            return;

        // Pressed from anywhere but chase, the key means "show me this" rather than nothing: switch
        // camera and switch the mode on together. The guard is the one CameraChaseState.EnterState
        // imposes on itself — it diverts to the orbit camera when it is not following an aircraft.
        if (cam.currentState != cam.chaseState)
        {
            if (cam.followingUnit is not Aircraft)
                return;

            _on = true;
            Reset();
            cam.SwitchState(cam.chaseState);
            return;
        }

        _on = !_on;

        // Only on the way in. Clearing it on the way out would drop the lag the model is currently
        // carrying — and since the blend is still at 1 that frame, the camera would jump to the
        // unlagged placement before starting to hand back.
        if (_on)
            _settled = false;
    }

    /// <summary>
    /// Replaces the placement <c>CameraChaseState.UpdateState</c> has just computed. Runs after it
    /// rather than instead of it so the FoV, the zoom, the numpad presets, the doppler and the wind
    /// noise all keep working, and so the vanilla result is available to blend against.
    /// </summary>
    /// <param name="zoom">
    /// The chase state's own <c>viewDistAdjust</c>, applied the same way vanilla applies it, so the
    /// zoom axis still lengthens the boom in this mode.
    /// </param>
    /// <param name="fovAdjust">
    /// The chase state's own <c>FOVAdjustment</c> — the player's FoV axis — so it keeps working on
    /// top of this mode's own field of view.
    /// </param>
    /// <returns>True when the camera was moved, so the caller can re-run terrain avoidance.</returns>
    internal static bool Apply(CameraStateManager cam, float zoom, float fovAdjust)
    {
        if (!Plugin.Enabled.Value || !Plugin.CinematicChaseCam.Value)
        {
            Reset();
            return false;
        }

        if (cam == null || cam.cameraPivot == null || cam.followingUnit == null
            || cam.followingRB == null || cam.followingUnit.definition == null)
        {
            return false;
        }

        // Switched off and already fully handed back: leave the transform completely alone, so with
        // the mode idle this costs nothing and changes nothing.
        if (!_on && _blend <= 0f)
        {
            _settled = false;
            return false;
        }

        Transform aircraft = cam.followingUnit.transform;
        Vector3 velocity = cam.followingRB.velocity;
        float dt = Time.deltaTime;

        // The pivot rather than the aircraft, because the chase state moves it off the aircraft while
        // a detached cockpit is tumbling away and hands the camera over to it. Following the airframe
        // there would leave the camera on the wreck instead of on the part being watched.
        Vector3 origin = cam.cameraPivot.position;

        Vector3 vanillaPosition = cam.transform.position;
        Quaternion vanillaRotation = cam.transform.rotation;

        UpdateAnchor(aircraft, velocity, dt);

        float stretch = UpdateStretch(velocity, dt);
        float cruise = UpdateCruiseSpeed(velocity.magnitude, dt);

        Quaternion boom = BoomRotation();
        Vector3 target = boom * BoomOffset(cam.followingUnit.definition, cruise, stretch, zoom);

        UpdateOffset(target, velocity, dt);
        UpdateRotation(dt);

        // MoveTowards rather than a damped approach: an ease that never quite arrives would leave
        // the mode permanently a few percent short of itself.
        _blend = Mathf.MoveTowards(_blend, _on ? 1f : 0f, dt / TransitionTime);
        float t = Mathf.SmoothStep(0f, 1f, _blend);

        // Before the guard, which measures against the FoV and so has to see the one this frame will
        // actually render at rather than the one vanilla left behind.
        ApplyFieldOfView(cam.mainCamera, fovAdjust, dt, t);
        ApplyFramingGuard(cam.mainCamera);

        cam.transform.SetPositionAndRotation(
            Vector3.Lerp(vanillaPosition, origin + _offset, t),
            Quaternion.Slerp(vanillaRotation, _rotation, t));

        _settled = true;
        return true;
    }

    /// <summary>
    /// Damps the anchor direction — the one the boom hangs off — towards the flight path.
    ///
    /// This single time constant is what the mode is: it decides how far the aircraft is allowed to
    /// rotate away from the camera before the camera comes after it.
    /// </summary>
    private static void UpdateAnchor(Transform aircraft, Vector3 velocity, float dt)
    {
        float speed = velocity.magnitude;
        Vector3 nose = aircraft.forward;

        Vector3 flight = speed > 0.001f
            ? Vector3.Slerp(nose, velocity / speed, Mathf.InverseLerp(SlowSpeed, FlyingSpeed, speed))
            : nose;

        // Anchoring to the nose instead is what the vanilla camera does, so this doubles as the dial
        // between the two behaviours.
        Vector3 wanted = Vector3.Slerp(nose, flight, Mathf.Clamp01(CinematicPresets.FlightPathAnchor));

        _anchor = _settled
            ? Vector3.Slerp(_anchor, wanted, Damping(CinematicPresets.BoomLag, dt)).normalized
            : wanted;
    }

    /// <summary>
    /// Builds the boom frame from the anchor with the world's up as its reference, which is what
    /// keeps the horizon level: the frame's own up always lies in the vertical plane through the
    /// anchor, so no roll can enter.
    ///
    /// <c>Quaternion.LookRotation(dir, Vector3.up)</c> would do the same until the aircraft goes
    /// vertical, where the two arguments become parallel and the result is undefined — and a loop
    /// passes straight through that. Constructing the basis by hand makes the degenerate case
    /// nameable: hold the azimuth the camera had a frame ago rather than let it spin arbitrarily.
    /// </summary>
    private static Quaternion BoomRotation()
    {
        Vector3 right = Vector3.Cross(Vector3.up, _anchor);

        if (right.sqrMagnitude < 0.0001f)
            right = _lastRight;

        right.Normalize();
        _lastRight = right;

        return Quaternion.LookRotation(_anchor, Vector3.Cross(_anchor, right));
    }

    /// <summary>
    /// How much longer the boom is right now than its resting length, because the aircraft is turning
    /// away from a camera that carries its own momentum.
    ///
    /// This is the mechanism behind the most recognisable moment in the reference footage: through a
    /// hard pull the aircraft visibly shrinks, then swells again as the camera catches up. It is
    /// deliberately driven by acceleration and not by speed — the aircraft is *decelerating* through
    /// that pull, so anything keyed to speed moves the camera the wrong way.
    ///
    /// Only the path-normal part of the acceleration counts. Accelerating along the flight path just
    /// makes the aircraft faster and the camera comes with it; it is turning that swings the aircraft
    /// off the line the camera was already travelling.
    ///
    /// Computed as an explicit length rather than by pushing the position spring around, which is how
    /// a first version did it. That version was both far too weak to see and impossible to tune, since
    /// its strength fell out of <c>PositionSmoothing</c> — a setting about something else entirely.
    /// </summary>
    private static float UpdateStretch(Vector3 velocity, float dt)
    {
        // Clamped because this is a difference of two sampled velocities, not an accelerometer: a
        // collision, a respawn or one dropped frame can put an arbitrarily large step through it, and
        // unclamped that would fling the camera off in a single frame. Twenty g is past anything an
        // airframe survives, so the limit only ever catches the artefacts.
        Vector3 acceleration = dt > 0f
            ? Vector3.ClampMagnitude((velocity - _lastVelocity) / dt, 200f)
            : Vector3.zero;

        _lastVelocity = velocity;

        Vector3 turning = acceleration - Vector3.Project(acceleration, _anchor);
        float target = turning.magnitude / 9.81f * CinematicPresets.StretchPerG;

        _stretch = _settled ? Mathf.Lerp(_stretch, target, Damping(StretchLag, dt)) : target;
        return _stretch;
    }

    /// <summary>Follows the aircraft's speed slowly, for the pullback. See <see cref="CruiseSpeedLag"/>.</summary>
    private static float UpdateCruiseSpeed(float speed, float dt)
    {
        _cruiseSpeed = _settled ? Mathf.Lerp(_cruiseSpeed, speed, Damping(CruiseSpeedLag, dt)) : speed;
        return _cruiseSpeed;
    }

    /// <summary>
    /// Where the camera wants to sit in the boom frame: behind, and a little above so the aircraft
    /// is seen slightly from the top rather than edge on.
    ///
    /// The base length is the game's own <c>orbitDist</c> formula, so an aircraft of any size gets a
    /// boom proportional to it, and the config scales that rather than replacing it. On top of that
    /// go two independent terms: a gentle pullback for an aircraft that is simply travelling fast,
    /// and the stretch a hard turn puts into it. Both move the camera rather than widening the FoV,
    /// so neither can fight the player's FoV axis or the weapon-FoV workaround.
    ///
    /// The stretch is added in metres, after the zoom, because it is a physical falling-behind rather
    /// than a change to how long the boom is meant to be.
    /// </summary>
    private static Vector3 BoomOffset(UnitDefinition definition, float cruiseSpeed, float stretch, float zoom)
    {
        float size = Mathf.Max(definition.length, definition.width * 0.7f)
                     + Mathf.Max(definition.width, definition.length * 0.7f);

        float fast = Mathf.InverseLerp(Plugin.CineSpeedLow.Value, Plugin.CineSpeedHigh.Value, cruiseSpeed);
        float distance = size
                         * Mathf.Max(Plugin.CineDistance.Value, 0.1f)
                         * (1f + CinematicPresets.SpeedPullback * fast)
                         * (1f + Mathf.Max(zoom, 0f));

        return new Vector3(0f, distance * Plugin.CineHeight.Value, -(distance + stretch));
    }

    /// <summary>
    /// Eases the offset towards where the boom frame says it should be, while letting the camera fly
    /// a course of its own.
    ///
    /// That second part is what makes this read as a camera rather than as a rig bolted to the
    /// aircraft. Everything here is expressed relative to the aircraft — it has to be, because the
    /// game shifts the world under the camera — and the naive consequence of that is a camera pinned
    /// rigidly to the aircraft's position: whatever the aircraft does translationally, the camera
    /// does identically, so the aircraft can never move within the frame except by the boom rotating
    /// or the aim trailing. Reference footage of a barrel roll disagrees plainly, with the aircraft
    /// wandering across most of the frame.
    ///
    /// The fix is to feed forward a *smoothed* velocity instead of the instantaneous one. At a steady
    /// course the two are equal and the camera keeps station exactly, so it never falls the hundred
    /// metres behind that a plain world-space spring would at combat speed. Everything the aircraft
    /// does off that course — a jink, a corkscrew, a break turn — is the difference between them, and
    /// integrates straight into the offset before the spring reels it back in.
    /// </summary>
    private static void UpdateOffset(Vector3 target, Vector3 velocity, float dt)
    {
        if (!_settled)
        {
            _offset = target;
            _offsetVelocity = Vector3.zero;
            _course = velocity;
            return;
        }

        _course = Vector3.Lerp(_course, velocity, Damping(CinematicPresets.PathLag, dt));
        _offset += (_course - velocity) * dt;

        _offset = Vector3.SmoothDamp(
            _offset, target, ref _offsetVelocity,
            Mathf.Max(CinematicPresets.PositionSmoothing, 0.001f), float.PositiveInfinity, dt);
    }

    /// <summary>
    /// Aims at the aircraft, damped on its own much shorter time constant.
    ///
    /// The gap between this and the boom's lag is what produces the look: with one time constant the
    /// camera would only feel soft, while a slow arm under a quick aim lets the aircraft swing across
    /// the frame and be caught. <see cref="Plugin.CineFramingPitch"/> then aims a few degrees above it,
    /// which is what seats the aircraft below the centre of the frame — the boom's own height offset
    /// cannot do that, since aiming at the aircraft cancels it exactly.
    /// </summary>
    private static void UpdateRotation(float dt)
    {
        Vector3 look = -_offset;
        if (look.sqrMagnitude < 0.0001f)
            look = _anchor;

        // Looking very nearly straight up or down, world up stops being a usable reference for the
        // same reason it does in BoomRotation. Keep the roll the camera already had.
        Vector3 up = Mathf.Abs(Vector3.Dot(look.normalized, Vector3.up)) > 0.999f
            ? _rotation * Vector3.up
            : Vector3.up;

        Quaternion target = Quaternion.LookRotation(look, up)
                            * Quaternion.Euler(-Plugin.CineFramingPitch.Value, 0f, 0f);

        _rotation = _settled
            ? Quaternion.Slerp(_rotation, target, Damping(CinematicPresets.AimLag, dt))
            : target;
    }

    /// <summary>
    /// Gives the mode its own field of view.
    ///
    /// This is not a convenience. <see cref="Plugin.CineFramingPitch"/> is an angle, so how far below
    /// centre the aircraft sits depends on the FoV: six degrees is 40% of the way to the edge at 30
    /// and half that at 60. The whole geometry here was calibrated against a narrow view, and reading
    /// the game's external FoV — which players set for a wide situational view in ordinary chase —
    /// would hand this mode a composition it was never measured for.
    ///
    /// Written straight onto the camera rather than through <c>desiredFOV</c>, which the chase state
    /// lerps towards. Editing that would leak: <c>SwitchState</c> reseeds it from whatever the camera
    /// currently reads, so this mode's value would follow the player into the next camera. Overriding
    /// the finished result instead leaves no state to restore — stop writing and vanilla's own lerp
    /// takes the FoV back on its own.
    ///
    /// The player's FoV axis still applies on top, clamped to the same 20–120 the chase state uses.
    /// </summary>
    private static void ApplyFieldOfView(Camera camera, float fovAdjust, float dt, float blend)
    {
        if (camera == null || Plugin.CineFov.Value <= 0f)
            return;

        float wanted = Mathf.Clamp(Plugin.CineFov.Value + fovAdjust, 20f, 120f);
        _fov = _settled ? Mathf.Lerp(_fov, wanted, Damping(FovLag, dt)) : wanted;

        camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, _fov, blend);
    }

    /// <summary>
    /// Hard bound on how far the aircraft may drift from where the camera is pointing.
    ///
    /// Lag is the point of this camera, but two sources of it push the same way and compound: the
    /// aim trails the aircraft during a sustained pull, and <see cref="Plugin.CineFramingPitch"/> is
    /// already aiming above it. Through a loop that put the aircraft against the bottom edge with
    /// half a frame of empty sky over it — the drift had stopped being character and started being a
    /// framing failure.
    ///
    /// This only ever removes excess, so below the limit the lag behaves exactly as before and
    /// nothing about the feel changes. Expressed as a fraction of the vertical half-FoV rather than
    /// in degrees so it means the same thing at any zoom: at a narrow FoV a few degrees is most of
    /// the frame, at a wide one it is nothing.
    /// </summary>
    private static void ApplyFramingGuard(Camera camera)
    {
        if (camera == null)
            return;

        Vector3 toAircraft = -_offset;
        if (toAircraft.sqrMagnitude < 0.0001f)
            return;

        float limit = Mathf.Clamp01(CinematicPresets.FramingLimit) * camera.fieldOfView * 0.5f;
        float off = Vector3.Angle(_rotation * Vector3.forward, toAircraft);
        if (off <= limit || off < 0.001f)
            return;

        // Keeping the current up vector rather than the world's: the guard is about where the
        // aircraft sits in frame, and it has no business levelling a horizon the aim has already
        // decided about.
        Quaternion straight = Quaternion.LookRotation(toAircraft, _rotation * Vector3.up);
        _rotation = Quaternion.Slerp(_rotation, straight, (off - limit) / off);
    }

    /// <summary>
    /// Fraction of the remaining distance to cover this frame for a given time constant.
    ///
    /// Exponential rather than the <c>Lerp(a, b, k * dt)</c> the game uses throughout: that form
    /// converges at a rate that depends on the frame rate, so the same settings would feel different
    /// at 60 and at 144 fps. Here the camera trails by the same amount either way.
    /// </summary>
    private static float Damping(float tau, float dt) =>
        tau <= 0.0001f ? 1f : 1f - Mathf.Exp(-dt / tau);
}
