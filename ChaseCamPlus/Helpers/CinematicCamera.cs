using UnityEngine;

namespace ChaseCamPlus.Helpers;

/// <summary>
/// A cinematic chase camera, where the aircraft is allowed to rotate and drift inside the frame
/// instead of the world rotating around it.
///
/// Five things separate it from the vanilla chase state, all visible frame by frame in reference
/// footage:
///
/// <list type="number">
/// <item>The horizon never tilts, at any bank angle, in either direction, upright or inverted. The
/// camera's own up vector is simply the world's.</item>
/// <item>The camera's <em>position</em>, on the other hand, is carried all the way round by the roll.
/// Level, it sits above the aircraft and the aircraft rides low in the frame; ninety degrees of bank
/// puts it out to the side and the aircraft across the frame; a full roll walks the aircraft round a
/// circle centred on the middle of the screen. These first two look contradictory and are not: the
/// rig rolls, the horizon does not. See <see cref="UpdateRollReference"/>.</item>
/// <item>It aims along the boom rather than at the aircraft, which is what lets any of that be seen.
/// A camera that aims at what it is following re-centres the aircraft every frame, so the boom can
/// swing as much as it likes and only the background will move. See <see cref="UpdateRotation"/>.</item>
/// <item>The boom is anchored to the <em>flight path</em>, not to the nose. During a hard pull the
/// aircraft sits visibly nose-high in the frame for seconds at a time, which only happens if the
/// camera is lined up with where the aircraft is going rather than where it is pointing.</item>
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
///
/// The numbers live in <see cref="CinematicTuning"/>. There is no configuration: the
/// <c>Chase Camera Roll Toggle</c> binding chooses between this camera and the game's own, and
/// nothing else about it is adjustable.
/// </summary>
internal static class CinematicCamera
{
    /// <summary>Hand-over time from the vanilla placement on entering chase, in seconds.</summary>
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

    private static bool _on;
    private static float _blend;
    private static bool _settled;

    private static Vector3 _anchor;
    private static Vector3 _offset;
    private static Vector3 _offsetVelocity;
    private static Vector3 _lastVelocity;
    private static Vector3 _lastRight = Vector3.right;
    private static Vector3 _rollReference = Vector3.up;
    private static Quaternion _rotation = Quaternion.identity;
    private static Vector3 _course;
    private static float _stretch;
    private static float _cruiseSpeed;

    /// <summary>
    /// Whether this camera is the one in use. Says nothing about which camera <em>state</em> is live —
    /// callers that care (the turret, the HUD) are already inside a chase-only path.
    /// </summary>
    internal static bool Active => _on;

    /// <summary>
    /// Drops the smoothing state so the next frame places the camera outright instead of easing in
    /// from wherever the last chase session left it. Called when the chase state is entered.
    ///
    /// Deliberately does not touch <c>_on</c>. Which of the two cameras you chose is not part of the
    /// smoothing, so leaving the cockpit and coming back returns you to the one you were using.
    /// </summary>
    internal static void Reset()
    {
        _settled = false;
        _blend = 0f;
        _offsetVelocity = Vector3.zero;
        _stretch = 0f;
    }

    /// <summary>
    /// Reads the toggle binding. Mirrors the guards <see cref="CameraToggle"/> applies, so menus,
    /// the map, the radial menu, the pause screen and the camera picker all suppress it.
    ///
    /// A plain toggle, and nothing else: pressed outside the chase view it still swaps which camera
    /// you will get, but it does not move you to one. The key is a property of the chase camera
    /// rather than a way of reaching it, and <see cref="CameraToggle"/> is what gets you there.
    /// </summary>
    internal static void Poll()
    {
        if (Plugin.CameraRollActionId < 0)
            return;

        var player = GameManager.playerInput;
        if (player == null || !player.GetButtonDown(Plugin.CameraRollActionId))
            return;

        // The game's own "is the player actually flying" flag, checked by both the cockpit and chase
        // states before they read any input.
        if (!GameManager.flightControlsEnabled)
            return;

        CameraControlUI picker = SceneSingleton<CameraControlUI>.i;
        if (picker != null && picker.isOpen)
            return;

        _on = !_on;

        // Only on the way in. Clearing it on the way out would drop the lag the model is currently
        // carrying — and since the blend is still at 1 that frame, the camera would jump to the
        // unlagged placement before starting to hand back.
        if (_on)
            _settled = false;
    }

    /// <summary>
    /// Replaces the placement <c>CameraChaseState.UpdateState</c> has just computed. Runs after it
    /// rather than instead of it so the field of view, the zoom, the numpad presets, the doppler and
    /// the wind noise all keep working, and so the vanilla result is available to blend against.
    /// </summary>
    /// <param name="zoom">
    /// The chase state's own <c>viewDistAdjust</c>, applied the same way vanilla applies it, so the
    /// zoom axis still lengthens the boom in this mode.
    /// </param>
    /// <returns>True when the camera was moved, so the caller can re-run terrain avoidance.</returns>
    internal static bool Apply(CameraStateManager cam, float zoom)
    {
        if (cam == null || cam.cameraPivot == null || cam.followingUnit == null
            || cam.followingRB == null || cam.followingUnit.definition == null)
        {
            return false;
        }

        // Switched off and already fully handed back: leave the transform completely alone, so with
        // the game's own camera selected this costs nothing and changes nothing.
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
        UpdateRollReference(aircraft, dt);

        float stretch = UpdateStretch(velocity, dt);
        float cruise = UpdateCruiseSpeed(velocity.magnitude, dt);

        // The field of view the frame will actually render at, which the chase state has already
        // written by the time this runs — the player's External Field of View setting with their FoV
        // axis folded in. The composition is stated in fractions of the frame and the angles are
        // worked back out of this, so both follow the player's own view setting rather than fighting
        // it. Nothing here writes the field of view.
        float fov = cam.mainCamera != null ? cam.mainCamera.fieldOfView : 60f;

        Quaternion boom = BoomRotation();
        Vector3 target = boom * BoomOffset(
            cam.followingUnit.definition, cruise, stretch, zoom, RideHeight(fov));

        UpdateOffset(target, velocity, dt);
        UpdateRotation(dt, FramingPitch(fov));

        // MoveTowards rather than a damped approach: an ease that never quite arrives would leave
        // the mode permanently a few percent short of itself.
        _blend = Mathf.MoveTowards(_blend, _on ? 1f : 0f, dt / TransitionTime);
        float t = Mathf.SmoothStep(0f, 1f, _blend);

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

        Vector3 wanted = speed > 0.001f
            ? Vector3.Slerp(nose, velocity / speed, Mathf.InverseLerp(SlowSpeed, FlyingSpeed, speed))
            : nose;

        _anchor = _settled
            ? Vector3.Slerp(_anchor, wanted, Damping(CinematicTuning.BoomLag, dt)).normalized
            : wanted;
    }

    /// <summary>
    /// Damps the aircraft's own up vector. This is what carries the boom round in a roll, and it is
    /// the single thing that separates a camera the aircraft rolls <em>under</em> from one it rolls
    /// <em>beside</em>.
    ///
    /// Built on the world's up instead — which is what this camera did before it was measured
    /// properly — an aileron roll moves the camera not at all and the aircraft merely spins on the
    /// spot in the middle of the frame. Carried, the camera swings round the roll axis with the
    /// aircraft, and since it is riding above the aircraft rather than on its axis, the aircraft is
    /// walked round a circle in the frame: low when level, out to one side at ninety degrees of bank,
    /// high when inverted.
    ///
    /// None of that tilts the horizon. The camera's own up vector is the world's, and stays the
    /// world's — see <see cref="UpdateRotation"/>. The rig rolls; the picture does not.
    /// </summary>
    private static void UpdateRollReference(Transform aircraft, float dt)
    {
        Vector3 up = aircraft.up;

        _rollReference = _settled
            ? Vector3.Slerp(_rollReference, up, Damping(CinematicTuning.RollLag, dt)).normalized
            : up;
    }

    /// <summary>
    /// Builds the boom frame from the anchor and the aircraft's damped up vector, so the frame rolls
    /// with the aircraft.
    ///
    /// <c>Quaternion.LookRotation(dir, reference)</c> would do the same until the two arguments become
    /// parallel, where the result is undefined. Constructing the basis by hand makes the degenerate
    /// case nameable: hold the azimuth the camera had a frame ago rather than let it spin arbitrarily.
    /// With the reference carried by the aircraft it is barely reachable — an aircraft's up and its
    /// flight path are ninety degrees apart in anything but a stall — but a stall is exactly where a
    /// camera flipping over would be least welcome.
    /// </summary>
    private static Quaternion BoomRotation()
    {
        Vector3 right = Vector3.Cross(_rollReference, _anchor);

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
    /// its strength fell out of the position smoothing — a number about something else entirely.
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
        float target = turning.magnitude / 9.81f * CinematicTuning.StretchPerG;

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
    /// Where the camera wants to sit in the boom frame: behind, and above, by enough that the roll
    /// carries the aircraft round a visible circle rather than a point.
    ///
    /// The base length is the game's own <c>orbitDist</c> formula, so an aircraft of any size gets a
    /// boom proportional to it, and <see cref="CinematicTuning.Distance"/> scales that rather than
    /// replacing it. On top of that go two independent terms: a gentle pullback for an aircraft that
    /// is simply travelling fast, and the stretch a hard turn puts into it. Both move the camera
    /// rather than widening the field of view, so neither can fight the player's FoV axis or the
    /// weapon-FoV workaround.
    ///
    /// The stretch is added in metres, after the zoom, because it is a physical falling-behind rather
    /// than a change to how long the boom is meant to be.
    /// </summary>
    private static Vector3 BoomOffset(
        UnitDefinition definition, float cruiseSpeed, float stretch, float zoom, float height)
    {
        float size = Mathf.Max(definition.length, definition.width * 0.7f)
                     + Mathf.Max(definition.width, definition.length * 0.7f);

        float fast = Mathf.InverseLerp(CinematicTuning.SpeedLow, CinematicTuning.SpeedHigh, cruiseSpeed);
        float distance = size
                         * CinematicTuning.Distance
                         * (1f + CinematicTuning.SpeedPullback * fast)
                         * (1f + Mathf.Max(zoom, 0f));

        return new Vector3(0f, distance * height, -(distance + stretch));
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

        _course = Vector3.Lerp(_course, velocity, Damping(CinematicTuning.PathLag, dt));
        _offset += (_course - velocity) * dt;

        _offset = Vector3.SmoothDamp(
            _offset, target, ref _offsetVelocity,
            Mathf.Max(CinematicTuning.PositionSmoothing, 0.001f), float.PositiveInfinity, dt);
    }

    /// <summary>
    /// Turns the camera along the boom, damped on its own much shorter time constant than the boom's.
    ///
    /// Along the boom, not at the aircraft. Aiming at the aircraft is the intuitive choice and it
    /// quietly destroys everything the boom is doing: a camera that re-aims at what it follows puts
    /// it back in the middle of the frame every frame, so a rolled boom moves the background and
    /// nothing else, and the whole circle a roll is meant to walk the aircraft round collapses to the
    /// aircraft spinning on the spot. That the reference aims along the boom is provable without any
    /// dynamics at all: in steady level flight it seats the aircraft well below the centre, where a
    /// camera aiming at the aircraft would have it dead centre by construction.
    ///
    /// The gap between this lag and the boom's is what produces the look. With one time constant the
    /// camera would only feel soft; a slow arm under a quick aim lets the aircraft swing across the
    /// frame and be caught. <paramref name="framingPitch"/> then offsets the aim from the boom axis,
    /// which is what settles where in the frame the aircraft finally sits.
    ///
    /// The up vector is the world's, and that is the whole of why the horizon stays level — including
    /// when the boom frame this is hanging off has been rolled ninety degrees by the aircraft.
    /// </summary>
    private static void UpdateRotation(float dt, float framingPitch)
    {
        // Looking very nearly straight up or down, world up stops being a usable reference for the
        // same reason it does in BoomRotation. Keep the roll the camera already had.
        Vector3 up = Mathf.Abs(Vector3.Dot(_anchor, Vector3.up)) > 0.999f
            ? _rotation * Vector3.up
            : Vector3.up;

        Quaternion target = Quaternion.LookRotation(_anchor, up)
                            * Quaternion.Euler(-framingPitch, 0f, 0f);

        _rotation = _settled
            ? Quaternion.Slerp(_rotation, target, Damping(CinematicTuning.AimLag, dt))
            : target;
    }

    /// <summary>
    /// How far above the flight path the camera rides, as a fraction of the boom length.
    ///
    /// The composition is stated in the unit it was measured in — a fraction of the frame — and the
    /// ratio follows from the field of view, a point <c>f</c> of the frame height off centre sitting
    /// at <c>2f·tan(fov/2)</c>. That is the more honest unit for anything about where things sit on
    /// screen: a fixed ratio silently recomposes the shot every time the field of view moves, and the
    /// field of view here belongs to the player.
    ///
    /// It also fixes the size of the circle a roll walks the aircraft round, the two being the same
    /// angle measured from the boom axis.
    /// </summary>
    private static float RideHeight(float fov) =>
        2f * CinematicTuning.BoomRise * HalfFrameTangent(fov);

    /// <summary>
    /// Degrees the camera aims above the boom axis. See <see cref="RideHeight"/>.
    ///
    /// This comes out <em>negative</em>, and that is not a slip. Aiming at the aircraft leaves it
    /// dead centre and has to lift the aim to seat it anywhere else; aiming along a boom that is
    /// already riding above the aircraft starts with it too low, and framing means dropping the aim
    /// to bring it back up. Same convention, opposite sign, because the two aims start from opposite
    /// places.
    /// </summary>
    private static float FramingPitch(float fov)
    {
        float t = HalfFrameTangent(fov);
        float rise = Mathf.Atan(2f * CinematicTuning.BoomRise * t);
        float seat = Mathf.Atan(2f * CinematicTuning.Seat * t);
        return -(rise - seat) * Mathf.Rad2Deg;
    }

    /// <summary>Tangent of the vertical half-angle, which is the whole of the screen-fraction conversion.</summary>
    private static float HalfFrameTangent(float fov) =>
        Mathf.Tan(Mathf.Clamp(fov, 1f, 179f) * 0.5f * Mathf.Deg2Rad);

    /// <summary>
    /// Hard bound on how far the aircraft may drift from where the camera is pointing.
    ///
    /// Lag is the point of this camera, but two sources of it push the same way and compound: the
    /// aim trails the aircraft during a sustained pull, and the framing pitch has already moved it.
    /// Through a loop that put the aircraft against the bottom edge with half a frame of empty sky
    /// over it — the drift had stopped being character and started being a framing failure.
    ///
    /// This only ever removes excess, so below the limit the lag behaves exactly as before and
    /// nothing about the feel changes. Expressed as a fraction of the vertical half-FoV rather than
    /// in degrees so it means the same thing at any zoom: at a narrow field of view a few degrees is
    /// most of the frame, at a wide one it is nothing.
    /// </summary>
    private static void ApplyFramingGuard(Camera camera)
    {
        if (camera == null)
            return;

        Vector3 toAircraft = -_offset;
        if (toAircraft.sqrMagnitude < 0.0001f)
            return;

        float limit = Mathf.Clamp01(CinematicTuning.FramingLimit) * camera.fieldOfView * 0.5f;
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
