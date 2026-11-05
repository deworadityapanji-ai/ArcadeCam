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

For version 0.1.17, the suites pass 11,821 camera/orbit/world-aim-frame/input/HUD/overlay-origin checks, 51 checks using real BepInEx config serialization, and 3,259 native/third-party API, audio, camera integration and production-IL checks (15,131 total).

The last suite includes the locally installed WT Mouse Aim 0.68.0 DLL. That part is skipped with a diagnostic
when the optional mod is absent, so the count will then be lower. It binds the real aim/look accessors and
checks the actual WT input/GUI rewrite without executing its flight controller or changing its settings.

The last suite uses the installed game's actual input method and damage API, plus the built plugin's render-lifetime methods. It targets .NET Framework 4.7.2 to match the installed Harmony runtime. An optional second argument selects a different built ArcadeCam DLL, such as a Release build.

G-LOC checks inspect both native presentation methods and verify that only their camera-mode read is replaced, with branch labels and exception boundaries preserved. The remaining simulation, blackout trigger and recovery instructions stay intact. Unexpected camera-mode read counts leave the native method untouched with a warning. These checks do not prove Unity's actual post-processing appearance.

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

## G-LOC transition (0.1.13)

The player confirmed the gradual G-LOC transition was fixed in the 0.1.13 gameplay test.
Repeat these checks as regressions when testing the new orbit style.

- While piloting your own aircraft, sustain high G in each chase horizon style. Check for gradual loss of colour, darkening and tunnel vision before full blackout, with the native muffled audio.
- Reduce G before losing consciousness; the effects should recede. Also continue through full blackout and recovery, checking that native control loss and recovery still occur.
- Switch between cockpit and chase during onset, blackout and recovery. Check for a consistent effect without a flash, stale vignette or stuck audio filter.
- Enter native orbit/free view or spectate another aircraft while affected, then return. These unsupported external views retain their native reset behavior. Check ejection, aircraft changes and respawn for leftover effects.
- Test with the flight HUD hidden and during weapon-camera tracking/return. Check visibility of the G-LOC effect, input continuity and restoration; these rendering interactions need gameplay verification.

## Orbit style (0.1.14)

The player confirmed Orbit worked in the 0.1.14 gameplay test. A multiplayer chat-box flash during
weapon view was subsequently reported; that is tracked separately below.

- Select ArcadeCam's Orbit preset in Configuration Manager and enter ArcadeCam chase view. Compare the base framing with native Orbit: upright horizon, aircraft centered, horizontal travel following and native view smoothing, without cinematic roll-arm/drift/stretch. Bank, roll fully, climb, dive, loop, stop and reverse direction.
- Verify the controller flight stick remains independent of look input. Pan through multiple full circles, look above/below/behind, toggle or hold free look and test Center View, optional automatic centering and target-look release/return.
- Change LevelHorizonStyle between Orbit and Cinematic while looking away from center. Test the Horizon Toggle in banked/inverted flight, followed by recentering and a cockpit/chase roundtrip.
- Test aircraft/camera/hidden HUD attachment, targeting feed, zoom, distance/height changes, menus/maps, world-origin changes, ejection, aircraft changes and respawn.
- Use the weapon camera through launch, impact/wreckage and return. Check the previous orbit view/HUD restores and the aircraft remains controllable.
- Verify Orbit and edited Custom settings survive restart; existing presets retain their previous feel. Repeat the confirmed G-LOC onset/recovery check in Orbit.

## Multiplayer chat during weapon view (0.1.15)

The candidate fix protects the chat/feed's ScreenSpaceOverlay root canvases from scene-root
translation during FloatingOrigin.OriginShift while following the local aircraft in ArcadeCam chase.
This includes weapon view and its return. Chat remains visible and retains its native messages,
expiry, input and settings. World-space/mixed camera/physics hierarchies are excluded. A canvas
already corrected by Unity or independently moved by layout is left alone.

Automated checks cover large positive/negative shifts, shared roots, no-op/double compensation,
scene destruction, render-mode changes, invalid deltas and preservation of native exception behavior.
They do not reproduce Unity canvas batching or establish that every observed flash has this cause.

- In multiplayer with chat/feed text visible, enter weapon view and follow a missile far enough to trigger world-origin shifts. Check the chat box, kill feed and background for position/visibility flashes. Repeat in the cinematic and Orbit styles.
- Test launch, impact/wreckage, release/toggle cancellation and return. Check the flight HUD remains hidden according to WeaponCameraHideHud and restores its previous state afterward.
- Check incoming chat expiry and opening/closing the chat input. Test menus/map, cockpit switching and respawn for stuck UI or input focus. Repeat the G-LOC regression.
- The bounded log entries `Chat overlay canvas:` report the resolved root/mode; `Chat overlay: protected` records an actual correction and whether weapon view was active. If the flash persists, compare those entries with the event before attributing the remaining behavior to another mod.

## WT Mouse Aim and weapon audio (0.1.16)

Both features await gameplay confirmation. Automated checks verify the installed APIs, instruction/branch
preservation, opt-in/profile persistence, listener handover order and audio lifetime across render callbacks.
They do not prove physical mouse behavior, actual listener selection, perceptual audio or the live Harmony
integration inside Unity.

- With WTMouseAimSupport off, repeat normal controller flight/free look and camera switching. Toggle WT's own master switch and confirm ArcadeCam does not unexpectedly recenter or reset free look.
- With WT Mouse Aim 0.68.0 enabled (Enabled/WriteControl/CameraFollow), enable ArcadeCam's compatibility option. Enter chase in each horizon/style and verify mouse aiming moves the aircraft toward the marker while ArcadeCam frames it below the reticle. Compare distances/heights/FoV and test loops/poles and terrain collision.
- Test ArcadeCam free-look toggle/hold, WT's right-mouse freeze and the native Free Look binding. Verify the aim stays frozen while looking, then the camera returns smoothly. Use the look stick after mouse input and vice versa; check flight-stick/keyboard overrides remain WT-controlled and independent of view input.
- Test Center View, selected-target look/return, cockpit/native-orbit/chase switches, maps/menus, chat input, WT master toggles, compatibility toggles, respawn and aircraft changes. Check cursor visibility/capture and no stuck controls.
- Verify WT's overlay follows WeaponCameraHideHud while recording/master-switch indicators remain native; verify native G-LOC fades once instead of stacking WT's extra grey-out in chase.
- With WeaponCameraFollowAudio on, compare audible missile motor/flight sounds near/far from the aircraft. Test explosions, misses, supersonic flight, wreckage viewing and return at long range. Check native sound arrival/spatial direction and the transition back to aircraft audio.
- Test hold release, toggle return, immediate cancellation via camera/map/menu, aircraft loss and scene reload. Verify wind/rattle and the original listener restore without silence, double-listener warnings, duplicated explosions or unexpected volume changes. Repeat with audio following off.
- The base camera, aircraft controls and weapon damage should keep their previous behavior. Continue testing the previously reported multiplayer chat flash; 0.1.15's fix still awaits explicit confirmation.

## WT roll / held-heading regression (0.1.17)

The player confirmed that the 0.1.16 adapter worked, but the cursor/view followed aircraft rotation during
roll, including mouse-only flight. The active setup was AceCombat with LevelHorizonByDefault=false. The
adapter incorrectly used aircraft-up for that view; free-look and target-look paths could also inherit it.

MouseAimFrame now smooths a direction independently of horizon leveling and accepts no aircraft-up input.
MouseAimView uses that frame in every WT-supported preset. WT free look uses world up except for a
continuous transported reference near a vertical pole; target look uses world up. The patch does not write
WT's aim direction, replace its flight law or change its saved manual-override settings.

Regression tests hold a world heading through changes in the underlying rig, check orthogonality and a
level horizon, handle an antipodal heading change, and track two complete loops through both poles without
a one-frame horizon flip. Production-IL checks verify the actual WT view and free-look paths use that frame
and do not select aircraft-up from the active preset. Actual gameplay confirmation is still required.

- Repeat the reported mouse-only case in AceCombat, WT support on. Set a turn/aim heading, stop moving the mouse and let the aircraft bank toward it. The world aim and view horizon should hold while the aircraft rolls underneath.
- Sweep aim left/right/up/down during a bank, then test inverted flight and complete loops. Check mouse movement stays consistent with the visible view and the original heading is retained when input stops.
- Repeat with Cinematic and Orbit level styles, free-look hold/toggle/RMB, selected-target look, Center View and return from weapon view.
- Turn WT compatibility off and confirm normal controller camera roll/horizon behavior remains as configured. The existing native WT manual-reorientation policy is a separate behavior and is preserved.

Target/wreckage following depends on a known nearby target, observed damage and a visible surviving airframe or part. Damage timing can be ambiguous during simultaneous hits. If no suitable subject is available, the camera uses the short explosion view instead.
