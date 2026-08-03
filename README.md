# ChaseCamPlus

Makes the chase camera usable on mouse and keyboard: the virtual joystick works in third person, the
camera gains a free look it never had, and the flight HUD stops disappearing.

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

**Turret aiming in chase.** Manual turret aiming reads the active camera, but vanilla gates it on
being in the cockpit — so in chase the turret holds its last commanded heading and cannot be moved.
Aim off to one side, switch to chase, and it stays there for the rest of the flight; the crosshair
is not stale, the weapon really is stuck. With this on the turret tracks the camera and recentres
when free look is released, exactly as in the cockpit. Turning it off does not go back to the vanilla
behaviour: the turret is pinned to the nose instead, so it works like a fixed forward gun rather than
staying aimed somewhere you can neither see nor correct. Chase only — the free and orbit cameras
would drag the turret around with them.

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

Both are injected into the game's own action set, so they appear alongside the built-in controls.
Neither is bound by default; an unbound action simply never fires.

## Config

`BepInEx\config\ChaseCamPlus.cfg`

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. |
| `VirtualJoystickInChase` | `true` | Mouse drives the virtual joystick in chase. |
| `FreeLook` | `true` | Enable the hold-to-orbit free look. |
| `FreeLookSensitivity` | `1.0` | Multiplier on top of the game's View Sensitivity. |
| `FreeLookMaxPitch` | `85` | Vertical orbit limit in degrees. Kept under 90 so the camera cannot flip over the poles; values above 89 are clamped. `0` disables vertical movement. |
| `RecenterOnRelease` | `true` | Return to the default chase angle on release. `false` keeps the angle you left it at. |
| `InvertFreeLookPitch` | `false` | Flip the free look's vertical direction. Applied on top of the game's own invert-pitch view setting, so the chase orbit can go one way while the cockpit free look goes the other. |
| `FlightHudInChase` | `true` | Keep the flight HUD on in chase. |
| `MapInChase` | `true` | Keep the minimap alive in chase. Needs `FlightHudInChase`. |
| `TurretAimInChase` | `true` | Manually aimed turret follows the camera in chase, as it already does in the cockpit. Set false to pin it to the nose instead, like a fixed forward gun. |
| `HidePitchLadderInChase` | `true` | Hide the pitch ladder in chase. Its rungs and its horizon line are one scrolling texture, so this takes both. |
| `HideWaterlineInChase` | `true` | Hide the waterline — the fixed aircraft datum at the centre of the HUD — in chase. |
| `HitSoundOutsideCockpit` | `true` | Hit confirmation sound outside the cockpit. Follows the game's own *show hit markers* setting. |
| `FixWeaponFoV` | `true` | Workaround for a vanilla bug, see below. |

Config changes apply on the next launch.

## The FixWeaponFoV workaround

Selecting an unguided bomb while in an external view snaps the camera FoV to the
*cockpit* default and leaves it there, because `HUDBombingState` reaches for `defaultFoV` without
checking which camera is live. It never reverts, since of the nine HUD weapon states only that one
and the boresight touch the FoV at all. The boresight has the same hardcoded value and re-applies it
every frame, though only when the game's *zoom on boresight* setting is on.

The workaround applies the cockpit/external choice the game already makes everywhere else. Turn it
off once the game ships a fix, so the mod stops second-guessing the base game.

Without it, switching to the cockpit and back restores the correct FoV.

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
