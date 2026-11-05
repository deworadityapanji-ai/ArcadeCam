# ArcadeCam configuration

Use BepInEx Configuration Manager to adjust these settings. See the main README for installation, controls and presets.

## Settings

**Chase Defaults:** choose chase when entering third person, free look on entry, default level horizon,
and LevelHorizonStyle (Cinematic or Orbit).
Explicit choices in the game's camera picker are respected.

### Orbit style (0.1.14)

Choose **ArcadeCam > Preset > Orbit** to apply an orbit starting point, or set
**Chase Defaults > LevelHorizonStyle = Orbit** and use the level horizon. Orbit stays upright,
centers the aircraft and follows its horizontal travel direction. It retains the game's view smoothing
but skips cinematic roll-arm movement, inertial drift, boom stretch and extra speed/pitch motion.
Distance, height, zoom, free look, controller bindings, centering, target look, targeting feed,
HUD layout, weapon camera and the 0.1.13 G-LOC correction remain available.

The existing **ArcadeCam Horizon Toggle** switches between aircraft-relative chase and the selected
level-horizon style. **LevelHorizonStyle = Cinematic** restores the original cinematic option.
Changing the style while level horizon is active blends the base views and preserves manual viewing
direction. Center View recenters on the new orbit course; continuous horizontal free look is retained.
Orbit preset starts with no automatic centering, camera-attached left/right instruments and no extra
speed/pitch motion. Independent compass, waterline, targeting-screen and weapon-camera preferences
are preserved. Existing presets keep their original style; Custom saves/restores the new selection.
Existing installations default to Cinematic until Orbit is selected. The native camera picker still
offers the game's unmodified Orbit view; choose ArcadeCam's Orbit preset to get its added features.

### Other settings

**Chase Free Look:** ToggleFreeLook selects toggle/hold. KeepFreeLookAngle holds the viewing angle when
free look ends. CenteringEnabled is an independent switch; Centering Force sets strength from 0 to 1.
OnInputRelease waits until mouse/look-stick movement stops; force 1 snaps back. ConstantPull keeps
the earlier continuous-pull behavior. CenteringDelay controls the idle wait in seconds, from 0 to 1; Ace Combat and Ace Combat 8 apply 0.55 seconds (55% of the slider). CenteringDeadzone
adds a controller-only release deadzone. Free look can rotate continuously around the aircraft; angle smoothing crosses the rear seam without a stop. Vertical look stops 0.5 degrees short of either pole to prevent manual inversion, with the native camera tilt included in those limits. Near vertical flight, pan eases toward the camera up axis so world-up does not turn it into a roll-only control. Horizon toggles convert both the current and pending viewing directions into the new orbit frame throughout the hand-over, instead of reusing angle numbers against different axes. Automatic centering waits until that hand-over finishes. Force 0 still means no pull, even with the switch enabled.

**Chase Target Look:** enable native Look at Target, choose whether to move around the aircraft so
it sits between camera and enemy, and select centered or previous-angle return. TargetReturnSeconds
sets both the target-release and Center View return time, 0 to 2 seconds. Default 0.45; zero permits an instant return. Auto-centering
stays suspended until target tracking and its return transition have finished. An explicit Center
press eases back using the same duration and suppresses held target look until release. Automatic
release centering still uses the existing force behavior, including the full-force snap.

**Chase Position:** CameraDistanceScale (0.25 to 3) and CameraHeightOffset (-20 to 30 meters) apply to
both chase styles. Height uses aircraft up in normal chase and world up in level-horizon chase.
MinimumZoomFraction (0.1 to 1, default 0.25) permits inward movement on native Zoom View.
Very close settings can put the camera inside large aircraft; increase distance when needed.

**Camera Motion:** SpeedMotionEnabled adds bounded distance movement driven by speed and acceleration.
Acceleration lengthens the boom slightly; airbraking draws it inward. SpeedMotionStrength controls
its size. PitchFollowEnabled anticipates pitch rate, opening space above the nose while pulling up
and below it while pushing down. PitchFollowStrength controls sensitivity to normal maneuver rates; PitchFollowLimit
caps the angle. MotionResponse smooths those two effects with a continuous-velocity spring and filtered acceleration, without adding roll delay. Motion gain is stronger at ordinary speed and pitch rates while keeping the previous distance and angle limits. Pitch anticipation
is suspended while looking around, tracking a target, or returning from target look. Motion resets on
chase entry, uses aircraft-relative offsets, and passes through terrain avoidance.

**Chase HUD:** FlightHud controls the overlay and Map controls the minimap. LeftInstruments,
RightInstruments, Compass and Waterline have Aircraft, Camera and Hidden choices. Camera placement
moves complete flight-HUD widgets out of their native UI parents and includes referenced external readouts,
including speed and radar altitude. Aircraft placement explicitly follows projected nose position and bank; Camera placement stays upright at screen center. Native helmet-mounted speed/altitude/bearing duplicates are excluded and suppressed in chase, then restored on exit. Native instruments continue reading aircraft data. Layout,
parent, scale, sibling order and rotation are saved once for the active layout and restored on exit, native options refresh, canvas/size/scale changes, or attachment-mode changes. Native HUDApps keep their data references and continue refreshing while attached. The mod does not reparent instruments on every frame. Detached widgets retain their resolved rectangle size with fixed anchors, and ignore parent layout drivers until restored, so a stretched compass/waterline cannot expand against the full canvas.
Camera waterline is a view-center marker; it does not indicate actual nose direction. The compass
still reads aircraft heading. The map, weapons, target markers and warnings keep their native behavior. Final instrument placement runs after camera and combat-HUD LateUpdates, including other mods which defer target selection there.
Instrument presentation is stored in canvas-local coordinates so world-origin shifts and subsequent
screen-canvas movement cannot add an unwanted world-space compensation. A final canvas-render check
corrects detected late local-position/rotation changes and suppresses duplicate helmet readouts.
Diagnostic entries prefixed `HUD presentation:` report the canvas mode, a bounded sample of origin/
canvas movements and up to eight rate-limited drift events, without requiring a debug key or setting.
This targets the intermittent instrument flash; actual elimination still needs in-game confirmation.
PitchLadder and VirtualJoystickIndicator retain their individual switches. Existing boolean HUD
choices migrate as true = Aircraft and false = Hidden.

**Targeting Screen:** TargetingScreenEnabled shows the native cockpit targeting-camera feed in chase
view, enabled by default. The panel sits in the upper right with space left above it for weapon
selection. TargetingScreenXPercent moves it from left (0) to right (100); TargetingScreenYPercent
moves it from top (0) to bottom (100). TargetingScreenWidthPercent changes its width from 10 to
50 percent of the view, default 24. Positions use the available travel area so the whole panel
remains on screen. The image keeps the native feed's aspect ratio, including at different HUD scales.

Select a target using the normal game controls. This reuses only the sensor camera's own output; matching cockpit UV orientation is preserved. The panel waits for a completed native sensor render, then draws directly as a screen overlay without creating or rebuilding any HUD canvas. It stays visible while that sensor remains active even if sensor rendering and game frames have different timing, and hides when the native camera stops or its context is lost. It does not create a second camera or alter targeting/visibility.
The native sensor and its overlay retain their own behavior. No separate camera binding is needed.
It hides on target clear, sensor loss, native landing-camera mode, map/menu/input-blocked states,
cockpit/other views, spectator aircraft, and when FlightHud is off. The previous image is cleared
when hidden. Aircraft without a working native target camera do not get a synthetic replacement.
Camera presets leave the current targeting-panel choice intact; Custom saves its visibility and layout.
**Weapon Camera (prototype):** bind **ArcadeCam Weapon Camera** in the normal Flight Controls menu.
From chase view, launch a missile or drop a bomb, then hold the binding. WeaponCameraToggle changes
it to press-on/press-off. It selects your latest live registered weapon at activation and keeps that
weapon until you return; subsequent shots do not steal the view. No live weapon means no view change.
In hold mode, you can hold before firing to follow a missile as it leaves the aircraft. A Weapon Camera
press takes priority over the game's hardcoded number-pad chase presets, including Numpad 7's bottom
view. Unclaimed number-pad shortcuts and the editor H shortcut retain their normal behavior.
Activating or reactivating acquires the current missile position immediately, including while returning
from an earlier view. Thereafter the camera carries the missile's full translation and smooths only
the relative boom, so a fast or distant weapon cannot outrun it. The existing rear-quarter framing remains.

WeaponCameraCinematic defaults on: a smooth rear-quarter view looks toward the weapon, with modest
speed pullback and turn-responsive movement between flanks. Turning it off gives a simple trailing
view. WeaponCameraDistanceScale (0.5 to 3, default 1) and WeaponCameraFieldOfView (25 to 90, default 55)
control framing. On a confirmed native armed detonation, WeaponCameraImpactSeconds permits a short
orbit of the explosion point (default 0.75 seconds). WeaponCameraFollowHitTarget defaults on: if the
missile's nearby target receives pierce/blast damage around that detonation, the view follows the
damaged airframe or its largest visible surviving part for WeaponCameraWreckageSeconds (default 2,
range 0 to 5 seconds). A damaged target may survive; this is not a guaranteed kill camera. The view
uses only that target's existing parts, keeps its selected piece fixed, and returns if it disappears.
It follows the live translation without inheriting tumbling roll or changing wreckage physics/lifetime.
Unknown targets, misses and unavailable visible parts use the existing explosion-point view instead.
Damage confirmation accepts closely reordered local damage/detonation notifications; simultaneous
hits from other weapons can be ambiguous. No new damage attribution or kill claim is generated.
WeaponCameraImpactShake adds an angular kick when the tracked weapon has an armed explosion,
including misses, airbursts and terrain impacts (default 0.65, range 0 to 1; 0 disables it).
It no longer waits for damage confirmation. It fades out in 0.4 seconds and affects only the
rendered weapon camera. Target/wreckage following still requires observed damage.
Turning Cinematic off or setting ImpactSeconds to 0 skips aftermath viewing and shake; setting
WreckageSeconds to 0 or FollowHitTarget off retains the old explosion view. Named presets preserve
all these preferences, and Custom saves/restores them. The prototype does not predict impact or
freeze before a possible hit. Missing/despawned weapons return without inventing a detonation.
WeaponCameraReturnSeconds controls the smooth return to the live aircraft view (default 0.35).
Release/press again cancels even during the impact view. Holding through automatic return requires
releasing before starting another weapon view.

The pilot, camera mode and followed unit remain the aircraft. A render-time pose/FoV/near-clip override
is restored after the flight camera renders, so native flight input, aiming and chase updates keep
their aircraft context. The aircraft/pivot-local pose is restored before native camera updates as well
as after rendering, and nested renders cannot overwrite the saved aircraft pose. Weapon poses are
initialized only after the normal chase update. The native floating origin follows the rendered viewpoint; cached positions
use GlobalPosition. Native damage, detonation timing, weapon physics and network ownership are untouched.

WeaponCameraHideHud hides the native flight/combat HUD, helmet readouts and minimap throughout
weapon tracking, explosion/wreckage viewing and the smooth return. Each affected canvas's previous
enabled state is restored when the session ends or is cancelled. Screen-space overlay hiding lasts
past the 3D rendering callbacks; nested canvases are included. No HUD GameObjects, input components,
gauge updates or saved aircraft HUD choices are disabled. The targeting-screen panel is hidden during weapon view. Turning HideHud
off retains the aircraft HUD and its aircraft readings, rather than creating a missile-aiming HUD.
Camera switching, menus/map, aircraft loss/ejection and disabling the feature cancel immediately.
From 0.1.15, the multiplayer chat/feed stays visible with its screen-space canvas protected from
world-origin shifts while following your own aircraft in ArcadeCam chase, including weapon view.
This is independent of WeaponCameraHideHud. Chat input, message contents and native expiry are preserved.
Standard mounted missiles/bombs and launcher missiles use the native registration path; gun bullets,
saved spectator missiles and arbitrary cluster submunitions are not selected. Both host/client use
the executed native detonation callback; a missing callback disables the optional impact view only.
Live framing, rendering/other-mod compatibility, distant tracking and retained aircraft control still
require in-game testing. No additional key assignments are made automatically.
**Virtual Joystick / Other:** preserve mouse steering in chase, adjust free-look sensitivity, enable
manual turret aiming, fix external-view weapon FoV, and retain outside-cockpit hit confirmation sound.
These original ChaseCamPlus features remain available. A hidden joystick indicator does not disable steering.

## WT Mouse Aim compatibility (0.1.16, experimental)

Install WT Mouse Aim **0.68.0** separately, then enable **ArcadeCam > Compatibility > WTMouseAimSupport**.
In WT Mouse Aim, enable **General > Enabled**, **Control > WriteControl**, and **Camera > CameraFollow**.
Enter ArcadeCam chase using its binding or the normal third-person switch with ChaseViewByDefault enabled.
The compatibility switch defaults off to preserve controller setups. It can be changed in Configuration
Manager without editing WT's files. Other WT versions are left unsupported with a diagnostic until validated.

WT owns mouse capture and its flight instructor; ArcadeCam owns the chase composition, HUD and weapon view.
Both level-horizon styles and aircraft-relative chase remain available. WT's OrbitAimSmoothing controls
aim-view responsiveness. The adapter does not change the game's global camera mode or modify WT's flight law.
From 0.1.17, the WT aim view stays horizon-stabilized even with an aircraft-relative preset such as AceCombat.
Aircraft bank cannot roll its aiming/free-look frame. Heading smoothing is independent of horizon leveling;
near vertical flight transports the view's up axis continuously before easing back to the level horizon.
Presets still select underlying framing and motion. The ordinary controller view retains its usual horizon
behavior when WT compatibility is off. WT target look also uses a world-up reference.
Aim-view framing keeps the aircraft below the reticle instead of allowing the centered Orbit composition
to obscure it. Existing lower-aircraft compositions are retained; zoom and lateral framing remain adjustable.

Chase starts in aiming mode while the adapter is active. Use ArcadeCam Free Look (toggle/hold), the game's
Free Look binding, or WT's configured right-mouse freeze to look around while freezing the aim marker.
Mouse free look uses WT's smoothed raw delta, sensitivity and inversion, with ArcadeCam's FreeLookSensitivity
multiplier. A live controller look stick retains the normal Rewired path. Releasing free look returns toward
the aim using ArcadeCam's TargetReturnSeconds; selected-target look also freezes the aim until its return ends.
During weapon view the marker stays frozen while aircraft flight/manual overrides remain WT's responsibility.

WT's flight overlay respects WeaponCameraHideHud; its pre-overlay recording/master-switch indicators stay
native. WT's additional full-screen G-LOC fade is suppressed only in the supported ArcadeCam chase context,
where the native G-LOC warning already runs. Cockpit and native Orbit keep WT's original camera behavior.
Neither WT's DLL nor its saved settings are rewritten. There is no hard assembly dependency when it is absent.
Actual mouse/controller handoffs, loops, menus and recording behavior still require in-game verification.

## Weapon-view audio (0.1.16)

**Weapon Camera > WeaponCameraFollowAudio** defaults on. A separate temporary listener follows the finalized
weapon/impact/wreckage/return viewpoint for the entire audio frame. The original listener is disabled first,
then restored when weapon view ends. Native explosion distance/propagation and supersonic-sound position and
relative-velocity checks use that same audio viewpoint. Camera-only aircraft wind/rattle are temporarily muted;
their previous mute states return afterward. UI/radio channels and global volume/mixer choices are preserved.

The feature reuses native sound sources; it adds no explosion recordings, forced replay or volume boost.
The simulation camera, aircraft ownership and weapon damage stay intact. Multiple/no active listener contexts
fall back to native audio with a warning. Turn WeaponCameraFollowAudio off to retain the previous audio behavior.
Audio perception, Doppler and cancellation/scene-change recovery still require gameplay testing.

## G-LOC in chase view

From 0.1.13, chase view while following your own aircraft uses the game's native G-LOC warning:
gradual darkening, loss of colour, tightening vignette and muffled audio before full blackout.
This follows the pilot's simulated condition, including recovery, rather than an independent camera timer.
The game's G tolerance, loss of flight control and unconsciousness duration are preserved.
It applies in both chase horizon styles and is independent of HUD visibility and camera presets.
Cockpit behavior and spectator camera behavior remain native. Camera switching resets the presentation
when leaving supported views. No new binding or configuration change is required.
