namespace ArcadeCam.Helpers;

/// <summary>
/// Every number the cinematic chase camera is made of, in one place.
///
/// These were configuration once, eleven dials plus a preset selector, and none of it earned its
/// place: the values are not taste. They were measured frame by frame off reference footage of the
/// camera this mode reproduces, they only work as a set, and no combination a player could reach by
/// turning one of them was better than the set. A config file that offers eleven ways to make the
/// camera worse is not offering a choice.
///
/// So they live here as constants. Tuning means editing this file and rebuilding, which is the
/// honest cost of a camera whose settings are a reconstruction rather than a preference.
///
/// Two conventions worth knowing before touching anything:
///
/// <list type="bullet">
/// <item>Times are in seconds and are true time constants — the fraction of the remaining distance
/// covered per frame is <c>1 - exp(-dt/tau)</c>, so they mean the same thing at any frame rate.</item>
/// <item>Anything about where something sits on screen is a <em>fraction of the frame height</em>,
/// never an angle or a ratio. The angles are worked back out from whatever field of view the game is
/// rendering at, so the composition survives the player moving the External Field of View slider.
/// See <c>CinematicCamera.RideHeight</c>.</item>
/// </list>
/// </summary>
internal static class CinematicTuning
{
    /// <summary>
    /// Seconds the boom takes to swing round behind a change in the flight path.
    ///
    /// The single most important number here: it is what lets the aircraft rotate away from the
    /// camera and be chased rather than followed. Towards zero this approaches the vanilla feel of a
    /// rig welded to the airframe.
    /// </summary>
    internal const float BoomLag = 0.45f;

    /// <summary>
    /// Seconds the camera takes to bring its aim back where the boom points.
    ///
    /// Wants to stay well below <see cref="BoomLag"/>. The gap between the two is what lets the
    /// aircraft swing across the frame and then be caught, rather than the whole rig just feeling
    /// soft.
    /// </summary>
    internal const float AimLag = 0.18f;

    /// <summary>
    /// Seconds the boom's roll reference takes to follow the aircraft's own up vector.
    ///
    /// Measured as a phase lag of 0.15-0.2 s through reference rolls.
    /// </summary>
    internal const float RollLag = 0.12f;

    /// <summary>Time constant of the spring that pulls the camera onto the boom's placement.</summary>
    internal const float PositionSmoothing = 0.14f;

    /// <summary>
    /// Seconds the camera's own course takes to follow the aircraft's velocity.
    ///
    /// This is what stops the camera being pinned to the aircraft's position — see
    /// <c>CinematicCamera.UpdateOffset</c>. Zero would weld them together and no other number here
    /// could get the aircraft to move across the frame.
    /// </summary>
    internal const float PathLag = 0.30f;

    /// <summary>
    /// Metres the boom stretches per g of path-normal acceleration.
    ///
    /// The shrink-then-swell through a hard pull. Measured at about 8% of the boom in the reference,
    /// which on a boom of this length is well under a metre per g — an earlier reading of 3 was five
    /// times too strong and threw the aircraft to a dot.
    /// </summary>
    internal const float StretchPerG = 0.6f;

    /// <summary>
    /// Extra boom length at high speed, as a fraction of the base length.
    ///
    /// Measured directly: apparent wingspan falls from 800 px at cruise to 622 px under full
    /// afterburner, a boom about 1.3 times longer, ramping smoothly over some eight seconds with no
    /// step when the burner lights. Driven by a heavily smoothed speed, never the instantaneous one —
    /// an aircraft <em>decelerates</em> through a pull, so a speed-keyed boom draws in exactly when
    /// the reference has it falling back.
    /// </summary>
    internal const float SpeedPullback = 0.3f;

    /// <summary>Speed in m/s at or below which the boom is at its base length.</summary>
    internal const float SpeedLow = 120f;

    /// <summary>Speed in m/s at or above which the boom is fully pulled back.</summary>
    internal const float SpeedHigh = 350f;

    /// <summary>
    /// Base boom length, as a multiple of the game's own chase distance for the aircraft.
    ///
    /// Not metres, so that a trainer and a heavy attacker fill the same share of the frame: the game
    /// derives a size from each aircraft's own dimensions and this scales it. See
    /// <c>CinematicCamera.BoomOffset</c>, which uses the game's formula verbatim.
    ///
    /// This one genuinely is a compromise rather than a measurement. Boom length and field of view
    /// trade against each other for apparent size, and the reference's field of view was never
    /// recoverable from the footage — what was measured is where the aircraft sits, not how large it
    /// is. Since the camera now takes the player's External Field of View rather than imposing one,
    /// this is set for the middle of that slider's range. Longer if the aircraft looks too big.
    /// </summary>
    internal const float Distance = 0.6f;

    /// <summary>
    /// How far the boom's ride height carries the aircraft off the boom axis, as a fraction of the
    /// frame height — which is also the radius of the circle a full roll walks it round.
    ///
    /// Measured statically, and so free of any lag: in level flight the reference seats the aircraft
    /// 0.166 of the frame below centre with the horizon 0.065 above it, and the two add to the angle
    /// between the boom axis and the aircraft. Checked against a 90° bank, where it predicts a
    /// lateral offset of 0.129 of the frame width against 0.125 measured.
    /// </summary>
    internal const float BoomRise = 0.23f;

    /// <summary>
    /// Where the aircraft actually sits in steady level flight, as a fraction of the frame height
    /// below centre.
    ///
    /// Less than <see cref="BoomRise"/>, so the aim ends up pitched slightly <em>down</em> from the
    /// boom axis, and <c>CinematicCamera.FramingPitch</c> comes out negative. With the boom already
    /// carrying the aircraft below the axis, framing is a matter of giving some of that back rather
    /// than of creating it.
    /// </summary>
    internal const float Seat = 0.165f;

    /// <summary>
    /// How far the aircraft may drift from where the camera points before it is reined in, as a
    /// fraction of the way to the top or bottom edge of the frame.
    ///
    /// A fraction rather than an angle so it means the same at any field of view. At 1 it only
    /// catches the aircraft actually leaving the frame, which is all the reference ever needs — it is
    /// a safety net against a pathological manoeuvre, not part of the look.
    /// </summary>
    internal const float FramingLimit = 1f;
}
