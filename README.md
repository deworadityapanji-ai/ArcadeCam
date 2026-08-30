# ChaseCamPlus

Makes Nuclear Option's chase camera usable on mouse and keyboard: the virtual joystick works in third
person, the camera gains a free look it never had, the flight HUD stops disappearing, and there is a
second chase camera that keeps the horizon level.

## Features

- **Virtual joystick in chase view.** Vanilla only feeds the mouse into the stick while the camera is
  in the cockpit, and the stick is not disabled in chase so much as frozen while still commanding —
  switch to chase mid-turn and the aircraft holds that deflection. This keeps it working. Sensitivity,
  centering and invert-pitch come from the game's own Virtual Joystick settings.
- **Free look.** Chase is the only camera in the game that never reads the view axes. Hold the binding
  and the mouse orbits around the aircraft; release and it eases back. The virtual joystick freezes
  while the key is held, so the mouse is never doing two jobs at once.
- **Cockpit ↔ chase toggle.** One key. From any other camera — orbit, TV, free, relative — it returns
  to the cockpit rather than doing nothing.
- **Hold to look through the cockpit.** Hold a key to borrow the cockpit for the length of a gun pass;
  release and you are back in the chase view you left. Turret aiming works properly while you hold it.
- **Flight HUD and minimap in chase.** Vanilla hides the HUD in third person, and several later paths
  switch it off again; this restores it and keeps it that way. Six switches decide how much of it you
  get.
- **Turret aiming in chase.** Vanilla gates manual turret aiming on the cockpit, so in chase the
  turret holds its last heading and cannot be moved. With this on it tracks the camera; with it off it
  is pinned to the nose, like a fixed forward gun.
- **Hit confirmation sound outside the cockpit.** Only the sound is added; the on-screen marker stays
  cockpit-only.
- **A chase camera that does not roll.** See below.

## Install

Requires [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (tested on 5.4.23.5).

Drop `ChaseCamPlus.dll` into `BepInEx\plugins\ChaseCamPlus\`, then launch once to generate the config
and register the keybinds.

## Keybinds

Assign under **Settings → Controls → Flight Controls**. None is bound by default, and an unbound
action simply never fires.

| Action | Purpose |
| --- | --- |
| `Chase Cam Free Look` | Hold to orbit the camera around the aircraft |
| `Chase Cam Toggle` | Switch between cockpit and chase |
| `Cockpit View (Hold)` | Hold to drop into the cockpit, release to return to chase |
| `Chase Camera Roll Toggle` | Swap between the game's chase camera and the level-horizon one |

## The level-horizon camera

The game's chase rig is welded to the airframe: its offset is built in the aircraft's own axes and its
up vector *is* the aircraft's up, so the world rolls when you roll and the aircraft can never turn
within the frame.

*Chase Camera Roll Toggle* swaps in one built the other way round. The horizon stays level at any bank
angle while the camera's position is carried round by the roll, so an aileron roll walks the aircraft
round a circle in the frame instead of spinning it on the spot. The boom hangs off the flight path
rather than the nose and lags behind it, and the camera flies a course of its own, so the aircraft
rotates and drifts in front of it. Pull hard and you watch yourself go nose-high and swing across the
frame before the camera comes round. The boom lengthens with speed and stretches under g.

It is a viewing mode more than a flying one: it stops pointing where the aircraft does, so anything
read off the frame is unreliable. It suits a jet better than a rotorcraft, whose flight path and nose
part company at the slightest crab. Free look, the virtual joystick and the cockpit toggle all keep
working.

It has no settings of its own. The numbers behind it only work as a set, and they live in
`Helpers/CinematicTuning.cs` if you build from source. Its field of view comes from the game's own
**External Field of View** setting, which also decides how large the aircraft looks.

## Config

`BepInEx\config\ChaseCamPlus.cfg`. Thirteen settings in three sections, written as what you *see*, so
`true` always means "this is on screen". Changes apply on the next launch.

### `Chase HUD`

Applies to whichever chase camera is running.

| Key | Default | Meaning |
| --- | --- | --- |
| `FlightHud` | `true` | Keep the flight HUD on in chase. |
| `Map` | `true` | Keep the minimap alive. Needs `FlightHud`. |
| `TurretAim` | `true` | A manually aimed turret follows the camera. Off pins it to the nose. |
| `PitchLadder` | `false` | Show the pitch ladder. Its rungs and its horizon line are one texture, so they come and go together. |
| `Waterline` | `false` | Show the waterline, the fixed aircraft datum at the centre of the HUD. |
| `LeftInstruments` | `false` | Show the left cluster: airspeed, angle of attack and its indexer, Mach, g load, fuel. |
| `RightInstruments` | `false` | Show the right cluster: throttle or collective, rotor RPM, altitude, radar altitude, climb rate. |
| `Compass` | `false` | Show the heading ribbon across the top and the degree readout under it. |
| `VirtualJoystickIndicator` | `false` | Show the stretching line and the dot on its end. Drawing only — the stick keeps working. |

All six default to off, so out of the box third person is a clean frame: what is left is the part of
the HUD that is about the world rather than about the aircraft — reticles, the velocity vector, the
free-look diamond, unit markers, warnings — plus the minimap. Switch back on whatever you want to
read.

Two are worth leaving off even if you turn the rest on. The pitch ladder and the waterline are drawn
against the camera's axes, so in third person they say something untrue; and the heading ribbon is
plainly wrong on the level-horizon camera, which is free to point away from where the aircraft is
going.

**Warnings are never hidden.** The stall and rotor-RPM warnings, the radar warning receiver, the gear
indicator and the helicopter's hover indicator share those panels and stay put whatever these are set
to.

### `Other`

| Key | Default | Meaning |
| --- | --- | --- |
| `FreeLookSensitivity` | `1.0` | Multiplier on top of the game's own View Sensitivity, for the chase free look. |
| `FixWeaponFoV` | `true` | Stop unguided bombs and boresight zoom forcing the cockpit field of view on external views. |
| `HitSoundOutsideCockpit` | `true` | Play the hit confirmation sound outside the cockpit. Follows the game's own *show hit markers* setting. |

The last two work around vanilla bugs and should come out once the game fixes them.

There is no switch for the free look itself: it does nothing until you bind a key, so leaving *Chase
Cam Free Look* unbound is already the off position.

### `Virtual Joystick`

| Key | Default | Meaning |
| --- | --- | --- |
| `VirtualJoystick` | `true` | Mouse drives the virtual joystick in chase view, as it already does in the cockpit. |

## Known vanilla issues

**Blacking out in third person.** Pull hard enough for long enough and the pilot passes out, and the
screen goes fully black for three to six seconds. In the cockpit you see it coming — the edges darken,
colour drains, the audio muffles — because the game builds those warning effects only in the cockpit
view, while the blackout itself is applied whatever the camera is doing. Not a mod problem, but this
mod is what puts you out there, so it is worth recognising.

**Weapon field of view.** Selecting an unguided bomb in an external view snaps the field of view to
the cockpit default and never puts it back; boresight zoom does the same when *zoom on boresight* is
on. `FixWeaponFoV` applies the cockpit/external choice the game makes everywhere else. Without it,
switching to the cockpit and back restores it.

**Dying in chase.** Vanilla throws an exception every frame after the pilot dies in third person,
until something else moves the camera. This mod guards it.

## Compatibility

Rewired stores bindings by numeric action id, and the allocation scheme most Nuclear Option mods
inherit depends on plugin load order — installing or removing an unrelated mod can shift the number
and hand your binding to someone else's action. This mod derives its ids from a hash of the action
name, so they are the same every launch whatever else is installed.

If you run another mod that shows the HUD in third person, remove it: two patches writing the same HUD
state will fight, and which wins depends on load order.

Client-side. No new values reach the network — the same clamped control inputs travel the same path
the game already uses from the cockpit.

## Credits

The Rewired action-injection technique comes from
[TargetCamControl](https://github.com/9138noms/TargetCamControl); its BepInEx adaptation was taken
from [YawOnMouse](https://github.com/muji2498/YawOnMouse) (MIT).
