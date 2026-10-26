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

## Installation

1. Install BepInEx 5 for Nuclear Option. BepInEx Configuration Manager provides the settings interface.
2. Download the latest **ArcadeCam-v0.1.12.zip** from [Releases](https://github.com/deworadityapanji-ai/ArcadeCam/releases/latest).
3. With the game closed, extract the ZIP into the Nuclear Option game folder. The DLL should end up at `BepInEx/plugins/ArcadeCam/ArcadeCam.dll`.
4. Start the game, bind the ArcadeCam actions under **Settings > Controls > Flight Controls**, then choose a preset in **Configuration Manager > ArcadeCam > ArcadeCam > Preset**.

For a manual install, place the release's `ArcadeCam.dll` in that same plugin folder. Update by replacing the existing DLL with the game closed. Your saved settings are retained. Keep only one copy of ArcadeCam.dll in the plugins directory tree.

ChaseCamPlus does not need to be installed separately. If both are installed, ArcadeCam takes control of chase view while enabled. Turning **EnableArcadeCam** off and restarting lets the original mod run again.

## Controls

No new bindings are assigned automatically. Flight axes stay independent of camera look controls.

| Action | Function |
| --- | --- |
| ArcadeCam Free Look | Toggle free look, or use the optional hold mode. |
| ArcadeCam View Toggle | Switch between cockpit and chase view. |
| ArcadeCam Cockpit (Hold) | Use cockpit view temporarily while held. |
| ArcadeCam Horizon Toggle | Switch between aircraft-relative and level-horizon chase. |
| ArcadeCam Centering Toggle | Turn automatic centering on or off. |
| ArcadeCam Weapon Camera | Hold to follow the latest live missile/bomb; optional toggle mode. Hold before launching to watch its release. |
| Native Pan View / Tilt View / Zoom View | Use your existing view axes and zoom bindings. |
| Native Center View / Look at Target | Recenter in chase or follow the selected target. |

Keep holding Weapon Camera through impact to watch the explosion and, when available, the damaged target or wreckage. Release to return; in toggle mode, press again. **WeaponCameraHideHud** hides the native HUD during the full sequence. **WeaponCameraImpactShake** adjusts the explosion kick, including an off setting.

See [configuration details](docs/CONFIGURATION.md), [building from source](docs/BUILDING.md), and [testing notes](docs/TESTING.md).

## Credits

ArcadeCam is a fork of **[ChaseCamPlus by CarloApri](https://github.com/CarloApri/NO-ChaseCamPlus)**, shared with the original developer's **explicit permission**. Thank you to CarloApri for the original mod and the foundation that made this project possible. The original MIT license and third-party credits are retained.

## AI disclosure

The code for ArcadeCam's additions and changes was written using AI, specifically **GPT 6.1 Sol**. I provided the design direction, feature requests and in-game testing.
