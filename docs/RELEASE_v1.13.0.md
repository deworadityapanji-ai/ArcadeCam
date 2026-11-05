# ArcadeCam v1.13

## Added
- **Orbit camera option:** a steady, level-horizon view with ArcadeCam's HUD, controller free look, recentering, target tracking and weapon camera. Select the Orbit preset or the Orbit level-horizon style.
- **WT Mouse Aim compatibility:** optional integration with WT Mouse Aim 0.68.0. WT handles mouse aiming and its flight instructor; ArcadeCam handles the camera, HUD and weapon view. Enable Compatibility > WTMouseAimSupport and keep WT's Enabled, WriteControl and CameraFollow settings on.
- **Weapon-camera audio:** optional audio following during weapon tracking, impact/wreckage viewing and return, including native explosion distance/propagation and supersonic sound checks. WeaponCameraFollowAudio defaults on and restores aircraft audio afterward.

## Fixed / improved
- **Gradual G-LOC warning in chase:** restored the game's darkening, loss of colour, tightening vignette and muffled audio before full blackout. Native G tolerance, control cutoff, blackout duration and recovery are preserved.
- **Stable WT aiming during rolls:** the aiming view keeps a level horizon and holds its world heading as the aircraft banks, including with the AceCombat preset. Free look and target look use a stable reference too; vertical transitions are smoothed.
- **Multiplayer chat overlay:** added protection against chat/feed canvas movement during world-origin shifts, including weapon view, while keeping chat visible and functional.
- WT's flight overlay respects weapon-camera HUD hiding, while its recording/master-switch indicators retain their own behavior. The extra WT G-LOC grey-out is suppressed in ArcadeCam chase to avoid stacking fades.

## Installation
Download ArcadeCam.dll under Assets and place it in BepInEx/plugins/ArcadeCam. Requires BepInEx 5; use BepInEx Configuration Manager for settings. WT Mouse Aim 0.68.0 is an optional separate install; its compatibility switch defaults off.

Public version: v1.13 (DLL 1.13.0; release tag v1.13.0).
