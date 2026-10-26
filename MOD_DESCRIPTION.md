# ArcadeCam

**A camera and HUD mod for Nuclear Option, built around controller play and inspired by arcade flight games.**

ArcadeCam started as a personal project because I couldn't find a mod that quite matched the camera feel I wanted. It gradually grew into something I wanted to share with other players coming from arcade flight games and getting used to Nuclear Option's more advanced simcade flying.

The aim is a more familiar, customizable view of the action while keeping the game's flight model and systems intact.

## Features

- **Free look and target tracking:** toggle or hold to look around, follow your selected target, and smoothly recenter the camera.
- **Two horizon styles:** switch between a camera that follows the aircraft's roll and one that keeps the horizon level.
- **Adjustable framing and motion:** change camera distance, height and zoom, with optional movement responding to speed, acceleration, airbraking and pitch maneuvers.
- **Customizable HUD:** attach instruments, compass and waterline to the aircraft or camera, or hide them individually.
- **Targeting screen in chase view:** display your aircraft's existing cockpit targeting-camera feed in a movable panel.
- **Missile and bomb camera:** follow your latest launched weapon while retaining aircraft control, with explosion shake, a brief view of the damaged target or surviving wreckage when available, and a smooth return. The HUD can stay hidden throughout the sequence.
- **Configurable controls and settings:** bind actions through the game's Flight Controls menu and adjust settings through BepInEx Configuration Manager.

## Presets

| Preset | What it does |
| --- | --- |
| **NuclearOptionPlus** | Aircraft-relative horizon and instruments, with free look and no automatic centering or added speed/pitch motion. |
| **AceCombat** | Closer framing, camera-fixed instruments, speed-related camera motion and strong centering after you release the look input. |
| **AceCombat8** | The AceCombat setup with added pitch anticipation for more dynamic maneuver framing. |
| **WarThunder** | Level horizon, camera-fixed instruments and free look without automatic centering or added speed/pitch motion. |
| **Custom** | Save and return to your own edited setup. |

These are inspired starting points, not exact recreations of those games. You can adjust them freely, and your edits persist across restarts.

## Requirements and testing

Requires **BepInEx 5**. Use **BepInEx Configuration Manager** for the settings interface.

ArcadeCam was developed and playtested with a **gamepad**. **HOTAS and full mouse-and-keyboard setups have not been tested.** Feedback from those setups is welcome.

## Credits

ArcadeCam is a fork of **[ChaseCamPlus by CarloApri](https://github.com/CarloApri/NO-ChaseCamPlus)**, shared with the original developer's **explicit permission**. Thank you to CarloApri for the original mod and the foundation that made this project possible. The original MIT license and third-party credits are retained.

## AI disclosure

The code for ArcadeCam's additions and changes was written using AI, specifically **GPT 6.1 Sol**. I provided the design direction, feature requests and in-game testing.
