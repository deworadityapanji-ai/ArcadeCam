# Testing ArcadeCam

ArcadeCam is developed and playtested with a gamepad. HOTAS and full mouse-and-keyboard setups have not been tested. Automated checks supplement gameplay testing; they do not reproduce Unity's full rendering lifecycle or prove controller hardware behavior.

## Automated checks

Build the production Debug plugin first, as described in [BUILDING.md](BUILDING.md). From the repository root in PowerShell:

```powershell
. .\scripts\environment.ps1
& $script:DotnetExe run --project .\tests\FreeLookInputState.Tests\FreeLookInputState.Tests.csproj
& $script:DotnetExe run --project .\tests\PresetManager.Tests\PresetManager.Tests.csproj
& $script:DotnetExe run --project .\tests\ChasePresetInput.Tests\ChasePresetInput.Tests.csproj -- "$script:GameDir"
```

For version 0.1.12, the suites pass 4,864 camera/input/HUD/motion/visibility checks, 41 checks using real BepInEx config serialization, and 663 native input/impact API and production-IL checks.

The last suite uses the installed game's actual input method and damage API, plus the built plugin's render-lifetime methods. It targets .NET Framework 4.7.2 to match the installed Harmony runtime. An optional second argument selects a different built ArcadeCam DLL, such as a Release build.

`tests/PluginIsolation.Tests.ps1` tests the reversible plugin-isolation helper against a disposable fake game tree. It does not move the real game's plugins.

## Gameplay checks

- Verify controller flight and camera axes remain independent, and test free-look hold/toggle, centering, native target look, zoom and cockpit/chase switching.
- Check both horizon styles in banked, inverted and near-vertical flight, including switching styles while looking around.
- Test Camera/Aircraft/Hidden HUD attachment modes, mixed configurations, 16:10 and other aspect ratios, UI scaling, window resizing, world-origin shifts and busy missions.
- Open and close maps/menus, change aircraft, eject and respawn. Check free-look state, HUD layout and settings restore correctly.
- Check the native targeting-screen overlay with sensor changes and weapon-camera entry/exit.
- Follow missiles and bombs, including a pre-launch hold and distant reacquisition. Test armed explosions, misses, terrain impacts, available/unavailable wreckage, early releases and toggle returns.
- Verify HUD hiding lasts throughout weapon tracking, impact/wreckage viewing and return, then restores previous visibility. Confirm aircraft controls continue working while the HUD is hidden.
- Test presets, edited settings, Custom and restart persistence. Repeat with other camera/HUD plugins and multiplayer host/client sessions as applicable.

Target/wreckage following depends on a known nearby target, observed damage and a visible surviving airframe or part. Damage timing can be ambiguous during simultaneous hits. If no suitable subject is available, the camera uses the short explosion view instead.
