# ChaseCamPlus

Makes the chase camera usable on mouse and keyboard: the virtual joystick works in third person, the
camera gains a free look it never had, and the flight HUD stops disappearing. Adds a second,
cinematic chase camera and a hold-to-cockpit key for when you need to aim.

## What it does

**Virtual joystick in chase view.** Vanilla only feeds mouse movement into the virtual joystick while
the camera is in the cockpit. Everything downstream still runs in every camera mode, so in vanilla
chase the stick is not disabled — it is frozen while still commanding, and switching to chase
mid-turn leaves the aircraft holding that deflection. This lets the accumulation run in chase too.
Sensitivity, centering and invert-pitch come from the game's own Virtual Joystick settings.

**Free look.** Chase is the only camera state in the game that never reads the view axes. Hold the
binding and the mouse orbits the camera around the aircraft, which stays centred; release and it
eases back. The virtual joystick freezes while the key is held, so the mouse is never doing two jobs
at once.

**Cockpit ↔ chase toggle.** One key. From any other camera (orbit, TV, free, relative) it returns to
the cockpit rather than doing nothing — a key you press mid-fight should never be inert. It stays
quiet when there is nothing to switch to: no aircraft, dead pilot, camera picker open, or flight
controls disabled.

**Flight HUD and map in chase.** Vanilla hides the HUD in third person, leaving you with no
instruments and no stick indicator. This restores it in the camera positions it was designed for
(Back, Tail, wing roots, Belly) and keeps it that way — pausing, opening the leaderboard and several
other paths switch the HUD off and only restore it when in the cockpit. Opening the map full screen
hides the HUD, matching what the cockpit already does.

**Per-element HUD toggles.** Five switches decide how much of the HUD third person gets: the pitch
ladder, the waterline, the left instrument cluster, the right one, and the heading ribbon. Turn them
all on and what remains is the part of the HUD that is about the world rather than the aircraft —
reticles, velocity vector, free-look diamond, unit markers, warnings. Warnings are never hidden.
Each switch has a twin used only while the cinematic camera is running, so the two views can disagree.

**Turret aiming in chase.** Manual turret aiming reads the active camera, but vanilla gates it on
being in the cockpit — so in chase the turret holds its last commanded heading and cannot be moved.
Aim off to one side, switch to chase, and it stays there for the rest of the flight; the crosshair
is not stale, the weapon really is stuck. With this on the turret tracks the camera and recentres
when free look is released, exactly as in the cockpit. Turning it off does not go back to the vanilla
behaviour: the turret is pinned to the nose instead, so it works like a fixed forward gun rather than
staying aimed somewhere you can neither see nor correct. Chase only — the free and orbit cameras
would drag the turret around with them.

**Cinematic chase camera.** An optional second chase camera. The vanilla chase rig is welded to the
airframe — its offset is built in the aircraft's own axes and its up vector *is* the aircraft's up —
so the world rolls when you roll and the aircraft can never turn within the frame. This one hangs the
camera off the *flight path* instead, keeps the horizon level, gives the camera inertia of its own,
and lets the aircraft rotate and drift in front of it. Pull hard and you watch yourself go nose-high
and swing across the frame before the camera comes round.

It is a viewing mode more than a flying one. The camera stops pointing where the aircraft does, so
anything read off it is unreliable — which is why it has its own copy of every chase HUD setting,
including whether a manually aimed turret should follow it at all. Free look and the virtual joystick
both keep working, so you can still fly. Bind a key and press it to switch in and out, which eases
across rather than cutting.

**Hold to look through the cockpit.** Third person is the better view for flying and the worse one
for shooting — the cinematic camera especially, which stops pointing where the aircraft does at all.
Bind a key (a mouse button suits it) and hold it to borrow the cockpit for the length of a gun pass;
release and the camera goes straight back to whichever chase view you were in, cinematic or plain,
easing in rather than cutting. Manual turret aiming works properly for as long as you hold it, since
that is the view the game wrote it for. Nothing to configure — the binding is the switch, and it only
arms from chase, so it can never pull you out of a view you chose deliberately.

**Hit confirmation sound outside the cockpit.** Vanilla gates both the hit marker and its sound on
the cockpit view, so in chase you get no feedback until the target comes apart. Only the sound is
added; the marker stays cockpit-only.

## Install

Requires [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (tested on 5.4.23.5).

Drop `ChaseCamPlus.dll` into `BepInEx\plugins\ChaseCamPlus\`. Launch once to generate the config
and register the keybinds.

## Keybinds

Assign under **Settings → Controls → Flight Controls**:

| Action | Purpose |
| --- | --- |
| `Chase Cam Free Look` | Hold to orbit the camera around the aircraft |
| `Chase Cam Toggle` | Switch between cockpit and chase |
| `Cinematic Chase Cam` | Switch the cinematic chase camera in and out. Pressed from another camera it switches to chase as well |
| `Cockpit View (Hold)` | Hold to drop into the cockpit, release to return to the chase camera you came from |

All four are injected into the game's own action set, so they appear alongside the built-in
controls. None is bound by default; an unbound action simply never fires.

## Config

`BepInEx\config\ChaseCamPlus.cfg`

BepInEx writes the sections alphabetically, so the file will not be in the order below — it opens on
`Chase HUD` and ends on `Workarounds`. The grouping is what matters; use the section names to find
your way.

### `Config`

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. |

### `Virtual Joystick`

| Key | Default | Meaning |
| --- | --- | --- |
| `VirtualJoystickInChase` | `true` | Mouse drives the virtual joystick in chase. |

### `Chase HUD`

Applied while the ordinary chase camera is running. Every key here has a twin in `Cinematic Chase
HUD`, used instead when the cinematic camera is the one running.

| Key | Default | Meaning |
| --- | --- | --- |
| `FlightHudInChase` | `true` | Keep the flight HUD on in chase. |
| `MapInChase` | `true` | Keep the minimap alive in chase. Needs `FlightHudInChase`. |
| `TurretAimInChase` | `true` | Manually aimed turret follows the camera in chase, as it already does in the cockpit. Set false to pin it to the nose instead, like a fixed forward gun. |
| `HidePitchLadderInChase` | `true` | Hide the pitch ladder in chase. Its rungs and its horizon line are one scrolling texture, so this takes both. |
| `HideWaterlineInChase` | `true` | Hide the waterline — the fixed aircraft datum at the centre of the HUD — in chase. |
| `HideLeftInstrumentsInChase` | `false` | Hide the left cluster: airspeed, angle of attack and its indexer, Mach, g load, fuel. |
| `HideRightInstrumentsInChase` | `false` | Hide the right cluster: throttle or collective, rotor RPM, altitude, radar altitude, climb rate. |
| `HideCompassInChase` | `false` | Hide the heading ribbon across the top and the degree readout under it. |

The last three are off by default, unlike the pitch ladder and waterline: those two are drawn against
the camera's axes and so read wrongly in third person, while the instruments read correctly wherever
the camera is. They are there for anyone who wants the frame clear — turn all five on and what is
left is the part of the HUD that is about the world rather than the aircraft: reticles, the velocity
vector, the free-look diamond, unit markers and warnings.

**Warnings are never hidden.** The stall and rotor-RPM warnings, the radar warning receiver, the gear
indicator and the helicopter's hover indicator share those panels and stay put. Tidying the frame is
not worth being told about a stall too late.

Groups are matched on the **component type**, not on object names, because names are scene data and
differ between airframes — the same throttle arc is `throttleGauge_AB` on a fighter and
`throttleGauge` on a helicopter. So an aircraft added by a later update is covered without an edit.

### `Free Look`

| Key | Default | Meaning |
| --- | --- | --- |
| `FreeLook` | `true` | Enable the hold-to-orbit free look. |
| `FreeLookSensitivity` | `1.0` | Multiplier on top of the game's View Sensitivity. |
| `FreeLookMaxPitch` | `85` | Vertical orbit limit in degrees. Kept under 90 so the camera cannot flip over the poles; values above 89 are clamped. `0` disables vertical movement. |
| `RecenterOnRelease` | `true` | Return to the default chase angle on release. `false` keeps the angle you left it at. |
| `InvertFreeLookPitch` | `false` | Flip the free look's vertical direction. Applied on top of the game's own invert-pitch view setting, so the chase orbit can go one way while the cockpit free look goes the other. |

### `Feedback`

| Key | Default | Meaning |
| --- | --- | --- |
| `HitSoundOutsideCockpit` | `true` | Hit confirmation sound outside the cockpit. Follows the game's own *show hit markers* setting. |

### `Cinematic Chase Cam`

| Key | Default | Meaning |
| --- | --- | --- |
| `CinematicChaseCam` | `true` | Enable the cinematic chase camera. Needs a key bound to *Cinematic Chase Cam*; without one this setting alone does nothing. |
| `Preset` | `Manual` | `Manual`, `Stable` or `Cinematic`. The two named presets ignore the eight behaviour settings below entirely — see the table after this one. |
| `Fov` | `30` | Field of view for this mode, in degrees, easing in and out with it. `0` leaves the game's external FoV alone. It gets its own because `FramingPitch` is an angle: the same six degrees seats the aircraft twice as far down the frame at 30 as at 60, so a wide view undoes the composition the other settings were measured for. The FoV axis still works on top. |
| `BoomLag` | `0.55` | Seconds the camera takes to swing round behind a change in the flight path. The setting that defines the mode — it is what lets the aircraft be chased rather than followed. |
| `AimLag` | `0.22` | Seconds the camera takes to bring the aircraft back to where it is aiming. Wants to stay well under `BoomLag`; the gap between them is what makes the aircraft swing across the frame and get caught. |
| `PositionSmoothing` | `0.18` | Seconds to settle into the position the boom asks for. Ordinary smoothing — the character comes from `BoomLag`. |
| `PathLag` | `0.25` | Seconds of inertia the camera has in its own flight path. What lets the aircraft move *around* inside the frame rather than only rotating in place: at `0` the camera is pinned to the aircraft's position and no other setting can free it. The size of the wander is this times `PositionSmoothing`, so it grows faster than it looks — the default is restrained on purpose. |
| `FlightPathAnchor` | `1.0` | Where the boom hangs from: `1` the flight path, `0` the nose. The flight path is why the aircraft sits nose-high through a hard pull; the nose is steadier through an aileron roll, which genuinely dishes the flight path. |
| `StretchPerG` | `2.0` | Metres the boom stretches per g pulled — what makes the aircraft shrink in a hard turn and swell again as the camera catches up. Only turning counts; accelerating along the flight path takes the camera with it. `0` gives a boom of fixed length. |
| `Distance` | `1.35` | Boom length as a multiple of the game's own chase distance, itself derived from the aircraft's size. |
| `Height` | `0.14` | How far above the flight path the camera rides, as a fraction of the boom length. Sets how much of the aircraft's top you see; it does not move it within the frame. |
| `FramingPitch` | `6` | Degrees the camera aims above the aircraft, which is what seats it below the centre of the frame. `0` centres it. |
| `FramingLimit` | `0.75` | How far the aircraft may drift from where the camera points before it is reined in, as a fraction of the way to the frame edge. A fraction rather than an angle, so it means the same at any FoV. Only removes excess — below the limit nothing changes. |
| `SpeedPullback` | `0.35` | Extra boom length when the aircraft is simply travelling fast, as a fraction added between `SpeedLow` and `SpeedHigh`. Reads a heavily smoothed speed on purpose: this is about the aircraft's regime, and `StretchPerG` is what answers to manoeuvres. Done by moving the camera, not by widening the FoV, so it cannot fight the FoV axis or `FixWeaponFoV`. |
| `SpeedLow` | `120` | Speed in m/s at or below which the boom is at its base length. |
| `SpeedHigh` | `350` | Speed in m/s at or above which it is fully pulled back. |

#### Presets

| | `Stable` | `Manual` (defaults) | `Cinematic` |
| --- | --- | --- | --- |
| `BoomLag` | 0.35 | 0.55 | 0.80 |
| `AimLag` | 0.15 | 0.22 | 0.30 |
| `PositionSmoothing` | 0.12 | 0.18 | 0.22 |
| `PathLag` | 0.10 | 0.25 | 0.45 |
| `FlightPathAnchor` | 0.60 | 1.00 | 1.00 |
| `StretchPerG` | 1.0 | 2.0 | 3.0 |
| `SpeedPullback` | 0.25 | 0.35 | 0.45 |
| `FramingLimit` | 0.60 | 0.75 | 0.88 |

Presets cover only how the camera *behaves*. `Fov`, `Distance`, `Height`, `FramingPitch` and the
speed band are the rig and the composition — taste and screen, not degrees of drama — so they stay
yours in all three. Each of the eight settings above repeats its preset values in the config file, so
you can copy a preset into `Manual` and adjust from there.

The one that does most of the work in `Stable` is `FlightPathAnchor` at 0.6: hanging the boom partly
off the nose is what stops a roll moving the camera, since a roll dishes the flight path but barely
moves the nose. It costs some of the nose-high look through a hard pull, which is the right trade for
a preset called Stable.

### `Cinematic Chase HUD`

The same eight keys as `Chase HUD`, applied instead whenever the cinematic camera is running, so
the two views can have different HUDs without editing the config between them.

`HideCompassInChase` is worth turning on here before anywhere else: this camera is free to point
somewhere other than the aircraft's heading, and a compass drawn across a view that is not aligned
with it is the most obviously wrong thing on the screen.

`TurretAimInChase` is the one to think about. This camera's forward runs back towards the aircraft
rather than out ahead of it, so a turret slaved to it ends up aimed at the ground below you — not a
bug, just what a camera that has stopped being a sight does. Set it `false` to pin the turret to the
nose while this camera is running.

### `Debug`

| Key | Default | Meaning |
| --- | --- | --- |
| `DebugDumpHudHierarchy` | `false` | Write the UI hierarchy around the flight HUD to the log. See below. |
| `DebugDumpKey` | `F10` | Key that triggers the dump. Only read when the above is `true`. |

### `Workarounds`

| Key | Default | Meaning |
| --- | --- | --- |
| `FixWeaponFoV` | `true` | Workaround for a vanilla bug, see below. |

Config changes apply on the next launch.

## Tuning the cinematic camera

Everything about how it feels is in three time constants, and they are meant to be read in order:

`BoomLag` decides how far the aircraft gets to rotate away before the camera reacts, `AimLag` decides
how quickly it is recovered once it has, and `PositionSmoothing` is only there to stop the boom
snapping. Raising all three together makes the camera soft rather than cinematic — the effect comes
from the *ratio*, not from the amounts.

`FlightPathAnchor` is the one worth understanding. At `1` the boom is lined up with where the
aircraft is going; at high angles of attack that leaves it visibly nose-up in the frame, which is the
single most recognisable thing about the reference footage. At `0` it lines up with the nose, which
is what vanilla does, and the mode collapses into a smoother version of the stock camera.

The two length terms answer different questions and are meant to be set independently.
`SpeedPullback` asks *is this aircraft fast*, off a speed smoothed over seconds; `StretchPerG` asks
*is it turning right now*. Keying boom length to instantaneous speed instead — which an early version
did — draws the camera **in** during a decelerating pull, which is backwards from every reference
clip: there the aircraft shrinks as it breaks away and swells as the camera runs it down.

`FramingLimit` is what makes a long `BoomLag` safe. Raise the lag for a lazier, more dramatic camera
and the guard stops the aircraft sliding off the edge of the frame while it does.

## The FixWeaponFoV workaround

Selecting an unguided bomb while in an external view snaps the camera FoV to the
*cockpit* default and leaves it there, because `HUDBombingState` reaches for `defaultFoV` without
checking which camera is live. It never reverts, since of the nine HUD weapon states only that one
and the boresight touch the FoV at all. The boresight has the same hardcoded value and re-applies it
every frame, though only when the game's *zoom on boresight* setting is on.

The workaround applies the cockpit/external choice the game already makes everywhere else. Turn it
off once the game ships a fix, so the mod stops second-guessing the base game.

Without it, switching to the cockpit and back restores the correct FoV.

## Blacking out in third person

Not a mod problem, but this mod is what puts you out there, so it is worth knowing: pull hard enough
for long enough and the pilot passes out, and the screen goes fully black for three to six seconds.

In the cockpit you see it coming — the edges darken, colour drains, the audio muffles. In any
external view you do not. The game builds those warning effects only `if (cameraMode == cockpit)`,
while the blackout itself is applied whatever the camera is doing, so third person takes you from a
perfectly clear picture to black with no warning at all.

Nothing here changes that. It is worth recognising rather than chasing as a bug in your graphics
settings.

## Two vanilla crashes this mod guards

`CameraChaseState` dereferences `followingUnit` without checking it in two places: `UpdateState`
guards on `followingRB` but then calls `CheckInput`, which opens with `followingUnit.definition`, and
`LeaveState` ends with `followingUnit.SetDoppler(true)`. Dying while in chase clears that field, so
vanilla throws once on the way out and then every frame afterwards until something else moves the
camera — several hundred exceptions in a row with the camera never positioned in between.

This mod skips the vanilla body when the unit has gone, and suppresses the one exception on the way
out, only in that exact case. Reported to the developers; the guards can come out once it is fixed.

## Debug aid

`DebugDumpHudHierarchy` is off by default. Enable it and press `DebugDumpKey` (F10 by default) to
write the UI hierarchy around the flight HUD to the BepInEx log — object names, components, disabled
ones marked, screen positions, plus the target designator's own state.

Which object owns a given HUD widget is scene data, not code, so this is the only way to name one
rather than guess at it. Leave it off unless you are chasing something down.

## Compatibility

Rewired persists bindings by numeric action id, and the allocation scheme most Nuclear Option mods
inherit depends on plugin load order — installing or removing an unrelated mod can shift the number
and hand your binding to someone else's action. This mod derives its ids from a hash of the action
name instead, so they are identical every launch regardless of what else is installed. If something
already occupies one, it falls back to sequential allocation and logs a warning.

Action names are prefixed with the plugin GUID, so name collisions cannot happen.

If you run another mod that shows the HUD in third person, remove it — two patches writing the same
HUD state will fight, and which one wins depends on load order.

## Multiplayer

No new values reach the network. The same clamped control inputs travel the same path the game
already uses from the cockpit.

## Credits

The Rewired action-injection technique comes from
[TargetCamControl](https://github.com/9138noms/TargetCamControl); its BepInEx
adaptation was taken from [YawOnMouse](https://github.com/muji2498/YawOnMouse) (MIT).
