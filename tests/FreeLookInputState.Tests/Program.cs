using UnityEngine;
using ArcadeCam.Helpers;

int checks = 0;

void Expect(bool actual, bool expected, string description)
{
    checks++;
    if (actual != expected)
        throw new Exception(description + $": expected {expected}, got {actual}");
}

var state = new FreeLookInputState();
Expect(state.Read(false, false, true, true, 0), false, "Starts inactive");
Expect(state.Read(true, true, true, true, 1), true, "First press enables toggle");
Expect(state.Read(true, true, true, true, 1), true, "Camera and physics agree on the same press");
Expect(state.Read(true, false, true, true, 2), true, "Holding does not toggle repeatedly");
Expect(state.Read(false, false, true, true, 3), true, "Release keeps toggle enabled");
Expect(state.Read(true, true, true, true, 4), false, "Second press disables toggle");
Expect(state.Read(true, true, true, true, 4), false, "Second press is not processed twice");

state = new FreeLookInputState();
Expect(state.Read(true, true, false, true, 0), true, "Hold mode activates while held");
Expect(state.Read(false, false, false, true, 1), false, "Hold mode deactivates on release");
Expect(state.Read(true, true, false, false, 2), false, "Hold mode respects blocked controls");

state = new FreeLookInputState();
Expect(state.Read(true, true, true, true, 1), true, "Toggle before opening menu");
Expect(state.Read(true, true, true, false, 1), false, "A menu suspends free look even in the same frame");
Expect(state.Read(true, true, true, true, 1), true, "Closing the menu restores the previous toggle without replaying the press");
Expect(state.Read(true, false, true, true, 2), true, "Held menu input cannot flip the restored toggle");
Expect(state.Read(false, false, true, true, 3), true, "Release after menu keeps the restored toggle enabled");
Expect(state.Read(true, true, true, true, 4), false, "A fresh press after menu toggles the restored choice off");

state.Reset(4);
Expect(state.Read(true, true, true, true, 4), false, "Leaving chase cannot re-arm on the same press");
Expect(state.Read(false, false, true, true, 5), false, "Returning to chase starts inactive");
Expect(state.Read(true, true, true, true, 6), true, "Fresh press after camera change works");
Expect(state.Read(false, false, false, true, 7), false, "Changing to hold mode drops the latch");
Expect(state.Read(true, true, false, true, 8), true, "Hold input works after live setting change");
Expect(state.Read(true, false, true, true, 9), false, "Changing back to toggle does not inherit held input");
Expect(state.Read(false, false, true, true, 10), false, "Toggle stays off until a new press");
Expect(state.Read(true, true, true, true, 11), true, "Toggle works after changing modes live");

// Map/menu suppression preserves both enabled and disabled choices.
state = new FreeLookInputState();
state.Reset(0, true, true);
for (int menu = 0; menu < 10; menu++)
{
    int frame = menu * 3 + 1;
    Expect(state.Read(false, false, true, false, frame), false, "Map navigation temporarily owns look input");
    Expect(state.Read(false, false, true, true, frame + 1), true, "Closing each map restores enabled free look automatically");
    Expect(state.Read(false, false, true, true, frame + 1), true, "Camera and physics agree after map close");
}
state.Reset(40);
Expect(state.Read(true, true, true, false, 41), false, "A menu button cannot enable a previously disabled toggle");
Expect(state.Read(true, true, true, true, 42), false, "A press held across closing is ignored even on a new frame");
Expect(state.Read(true, false, true, true, 43), false, "A longer hold after menu close still cannot toggle");
Expect(state.Read(false, false, true, true, 44), false, "Releasing menu input preserves disabled choice");
Expect(state.Read(true, true, true, true, 45), true, "Fresh flight input works after the menu press has been released");
state.Read(false, false, true, false, 46);
state.Reset(47);
Expect(state.Read(false, false, true, true, 48), false, "A genuine camera-session reset still clears suspended free look");
state.Reset(50, true, true);
state.Read(false, false, true, false, 51);
state.Read(false, false, false, false, 52);
Expect(state.Read(false, false, true, true, 53), false, "Changing input mode during a menu discards the old latch");
Expect(state.Read(true, true, false, false, 54), false, "Hold mode stays suppressed inside menus");
Expect(state.Read(true, false, false, true, 55), true, "Hold mode resumes when the held button returns to flight");
Expect(state.Read(false, false, false, true, 56), false, "Hold mode stops naturally on release");

void Near(float actual, float expected, string description)
{
    checks++;
    if (Math.Abs(actual - expected) > 0.00001f)
        throw new Exception(description + $": expected {expected}, got {actual}");
}

var blend = new TargetLookBlend();
Near(blend.Advance(false, 0.1f), 0f, "No target leaves the camera unmodified");
Near(blend.Advance(true, 0.125f), 0.5f, "Target acquisition blends in");
Near(blend.Advance(true, 0.125f), 1f, "Target acquisition completes");
Near(blend.Advance(true, 1f), 1f, "Long frame cannot overshoot tracking");
Near(blend.Advance(false, 0.0625f), 0.75f, "Release starts returning to manual view");
Near(blend.Advance(true, 0.0625f), 1f, "Reacquiring during release reverses smoothly");
Near(blend.Advance(false, 1f), 0f, "Losing target fully restores manual view");
Near(blend.Advance(true, -1f), 0f, "Negative delta does not corrupt the blend");
Near(blend.Advance(true, 0f), 0f, "Zero delta leaves state unchanged");
blend.Advance(true, 0.1f);
blend.Reset();
Near(blend.Weight, 0f, "Leaving chase resets target blending");
Near(blend.Advance(false, 0.1f), 0f, "A new chase session starts without a target override");

var lowFps = new TargetLookBlend();
var highFps = new TargetLookBlend();
for (int i = 0; i < 3; i++) lowFps.Advance(true, 1f / 30f);
for (int i = 0; i < 12; i++) highFps.Advance(true, 1f / 120f);
Near(lowFps.Weight, highFps.Weight, "Target handover timing is independent of frame rate");
Near(lowFps.Weight, 0.4f, "Elapsed time determines blend weight");
for (int i = 0; i < 3; i++) lowFps.Advance(false, 1f / 30f);
for (int i = 0; i < 12; i++) highFps.Advance(false, 1f / 120f);
Near(lowFps.Weight, 0f, "Release reaches manual view at low frame rate");
Near(highFps.Weight, 0f, "Release reaches manual view at high frame rate");

state.Reset(20, true, true);
Expect(state.Read(false, false, true, true, 20), true, "Chase entry can start toggle free look");
Expect(state.Read(true, true, true, true, 20), true, "Entry press cannot undo the default in the same frame");
Expect(state.Read(true, true, true, true, 21), false, "First later button press can disable automatic free look");
state.Reset(22, true, false);
Expect(state.Read(false, false, false, true, 22), false, "Automatic entry does not latch hold mode");
Expect(state.Read(true, true, false, true, 23), true, "Hold mode still works after entry");
state.Reset(24, false, true);
Expect(state.Read(false, false, true, true, 24), false, "Disabled entry preference starts inactive");

Near(CameraCentering.Decay(90f, 0f, 2f), 90f, "Zero centering force preserves the angle");
Near(CameraCentering.Decay(-90f, 0f, 2f), -90f, "Zero force preserves a negative angle");
Near(CameraCentering.Decay(90f, 1f, 0f), 90f, "Centering ignores zero delta");
Near(CameraCentering.Decay(90f, 1f, -1f), 90f, "Centering ignores negative delta");
Near(CameraCentering.Decay(0f, 1f, 2f), 0f, "Center remains centered");
Expect(CameraCentering.Decay(90f, 1f, 1f) < CameraCentering.Decay(90f, 0.25f, 1f), true, "A stronger slider pulls faster");
Expect(CameraCentering.Decay(-90f, 1f, 1f) > -90f, true, "Negative angles move toward zero");
Near(CameraCentering.Decay(90f, -1f, 1f), 90f, "Force is bounded below");
Near(CameraCentering.Decay(90f, 2f, 1f), CameraCentering.Decay(90f, 1f, 1f), "Force is bounded above");
float centered30 = 90f;
float centered120 = 90f;
for (int i = 0; i < 30; i++) centered30 = CameraCentering.Decay(centered30, 0.5f, 1f / 30f);
for (int i = 0; i < 120; i++) centered120 = CameraCentering.Decay(centered120, 0.5f, 1f / 120f);
checks++;
if (Math.Abs(centered30 - centered120) > 0.0002f) throw new Exception("Centering must be frame-rate independent");

Expect(ChaseEntryPolicy.PreferChase(true, true, true, false, true), true, "Third person can default to chase");
Expect(ChaseEntryPolicy.PreferChase(false, true, true, false, true), false, "Orbit stays available when the default is off");
Expect(ChaseEntryPolicy.PreferChase(true, false, true, false, true), false, "Existing external camera transitions are not redirected");
Expect(ChaseEntryPolicy.PreferChase(true, true, false, false, true), false, "Spectator cameras are not redirected");
Expect(ChaseEntryPolicy.PreferChase(true, true, true, true, true), false, "Explicit camera-picker choices are preserved");
Expect(ChaseEntryPolicy.PreferChase(true, true, true, false, false), false, "Menus and editor cameras are preserved");

Vector3[] targetDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down, new Vector3(1f, 2f, -3f) };
foreach (Vector3 direction in targetDirections)
{
    Vector3 offset = TargetCameraFraming.Offset(direction, Vector3.up, 50f);
    Near(Vector3.Dot(offset, direction.normalized), -50f, "Camera stays opposite the target across the aircraft");
    Expect(offset.sqrMagnitude >= 50f * 50f, true, "Framing keeps the camera outside the aircraft");
    Expect(float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z), false, "Overhead and rear targets produce finite offsets");
}
Vector3 rearOffset = TargetCameraFraming.Offset(Vector3.back, Vector3.up, 50f);
Expect(rearOffset.z > 0f, true, "A rear enemy moves the camera in front of our aircraft");
Vector3 normalOffset = TargetCameraFraming.Offset(Vector3.forward, Vector3.up, 50f);
Vector3 invertedOffset = TargetCameraFraming.Offset(Vector3.forward, Vector3.down, 50f);
Near(normalOffset.y, -invertedOffset.y, "Aircraft-relative framing respects inverted flight");
Near(TargetCameraFraming.Offset(Vector3.zero, Vector3.up, 50f).sqrMagnitude, 0f, "Coincident target direction is safe");

void ActionIs(CenteringAction actual, CenteringAction expected, string description)
{
    checks++;
    if (actual != expected) throw new Exception(description + $": expected {expected}, got {actual}");
}
var release = new CenteringInputState();
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, true, 1f, 0f, 0.1f), CenteringAction.None, "Full force never fights active look input in release mode");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, 0.05f, 0.02f), CenteringAction.None, "Mouse sample gaps respect the idle delay");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, 0.05f, 0.04f), CenteringAction.Snap, "Full force snaps when the stick is released");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, 0f, 0.1f), CenteringAction.None, "Idle full force does not reset the cinematic camera every frame");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, true, 1f, 0f, 0.1f), CenteringAction.None, "New stick movement rearms release centering");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, 0f, 0.1f), CenteringAction.Snap, "A second stick release snaps again");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 0.5f, 0f, 0.1f), CenteringAction.Pull, "Lower force recenters gradually after release");
ActionIs(release.Read(CenteringBehavior.ConstantPull, true, false, true, 1f, 0f, 0.1f), CenteringAction.Pull, "Constant mode retains the previous active-input pull");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, true, false, 1f, 0f, 0.1f), CenteringAction.None, "Target look suspends centering");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, false, false, false, 1f, 0f, 0.1f), CenteringAction.None, "Inactive free look preserves the retained view");
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 0f, 0f, 0.1f), CenteringAction.None, "Zero force disables release centering");
release.Reset();
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, 0f, 0f), CenteringAction.Snap, "Zero idle delay permits immediate release snap");
release.Reset();
ActionIs(release.Read(CenteringBehavior.OnInputRelease, true, false, false, 0.5f, 0.2f, -1f), CenteringAction.None, "Negative time cannot advance the idle timer");

Near(CameraGeometryMath.MinimumZoomAdjustment(0.25f), -0.75f, "Zoom can reach quarter boom distance");
Near(CameraGeometryMath.MinimumZoomAdjustment(1f), 0f, "The original closest distance remains selectable");
Near(CameraGeometryMath.MinimumZoomAdjustment(-1f), -0.9f, "Zoom lower bound stays positive");
Near(CameraGeometryMath.MinimumZoomAdjustment(3f), 0f, "Zoom limit cannot exceed the default minimum");
Vector3 closeView = CameraGeometryMath.AdjustOffset(new Vector3(0f, 10f, -100f), Vector3.up, 0.5f, 3f);
Near(closeView.z, -50f, "Distance slider moves the camera closer");
Near(closeView.y, 8f, "Height offset is added in meters");
Vector3 invertedView = CameraGeometryMath.AdjustOffset(Vector3.back * 100f, Vector3.down, 1f, 5f);
Near(invertedView.y, -5f, "Normal chase height respects aircraft up when inverted");
Near(CameraGeometryMath.AdjustOffset(Vector3.back * 100f, Vector3.up, 1f, 0f).z, -100f, "Default geometry preserves the original boom");

checks++;
if (HudAttachmentMigration.Parse("True", HudAttachment.Hidden) != HudAttachment.Aircraft) throw new Exception("Old visible HUD settings must stay aircraft-attached");
checks++;
if (HudAttachmentMigration.Parse("False", HudAttachment.Aircraft) != HudAttachment.Hidden) throw new Exception("Old hidden HUD settings must remain hidden");
checks++;
if (HudAttachmentMigration.Parse("Camera", HudAttachment.Aircraft) != HudAttachment.Camera) throw new Exception("Camera attachment must survive restart");
checks++;
if (HudAttachmentMigration.Parse("hidden", HudAttachment.Aircraft) != HudAttachment.Hidden) throw new Exception("HUD mode parsing is case insensitive");
checks++;
if (HudAttachmentMigration.Parse("99", HudAttachment.Aircraft) != HudAttachment.Aircraft) throw new Exception("Invalid numeric HUD modes must fall back safely");
checks++;
if (HudAttachmentMigration.Parse("bad", HudAttachment.Aircraft) != HudAttachment.Aircraft) throw new Exception("Invalid HUD settings must fall back safely");
checks++;
if (Enum.GetNames<HudAttachment>()[2] != "Hidden") throw new Exception("Hidden must be the last dropdown option");

var motion = new CameraMotionState();
motion.Advance(200f, 0f, false, 1f, false, 1f, 8f, 0f, 0.1f);
Near(motion.DistanceMultiplier, 1f, "Disabled speed motion preserves boom length");
Near(motion.PitchDegrees, 0f, "Disabled pitch anticipation preserves aim");
motion.Reset();
motion.Advance(200f, 100f, true, 1f, true, 0.12f, 8f, 0f, 0.1f);
Near(motion.DistanceMultiplier, 1.096f, "First sample has speed pullback without a false acceleration kick");
Near(motion.PitchDegrees, 8f, "Pitch anticipation respects the angle limit");
motion.Advance(204f, -100f, true, 1f, true, 0.12f, 8f, 0f, 0.1f);
Expect(motion.DistanceMultiplier > 1.2f, true, "Acceleration gently lengthens the boom");
Near(motion.PitchDegrees, -8f, "Pitch-down anticipation has the opposite sign");
motion.Advance(200f, 0f, true, 1f, true, 0.12f, 8f, 0f, 0.1f);
for (int i = 0; i < 5; i++) motion.Advance(196f - i * 4f, 0f, true, 1f, true, 0.12f, 8f, 0f, 0.1f);
Expect(motion.DistanceMultiplier < 1f, true, "Sustained airbraking smoothly draws the boom inward");
motion.Advance(0f, 999f, true, 1f, true, 1f, 20f, 0f, 0.01f);
Expect(motion.DistanceMultiplier >= 0.82f, true, "Collision speed steps cannot collapse the boom");
Near(motion.PitchDegrees, 20f, "Extreme angular rates remain bounded");
motion.Advance(99999f, 0f, true, 1f, false, 0f, 0f, 0f, 0.01f);
Expect(motion.DistanceMultiplier <= 1.3f, true, "Extreme speeds cannot extend the boom without bound");
float beforePause = motion.DistanceMultiplier;
motion.Advance(0f, 999f, false, 1f, false, 1f, 20f, 0f, 0f);
Near(motion.DistanceMultiplier, beforePause, "Paused time leaves the camera motion unchanged");
motion.Advance(0f, 0f, false, 1f, false, 1f, 20f, 0f, 0.1f);
Near(motion.DistanceMultiplier, 1f, "Disabling motion restores the native distance");
Near(motion.PitchDegrees, 0f, "Disabling pitch follow restores normal aim");
motion.Reset();
motion.Advance(200f, 0f, true, 1f, false, 0f, 0f, 0f, 1f);
Near(motion.DistanceMultiplier, 1.096f, "A long frame does not introduce a velocity-difference kick");
var motion30 = new CameraMotionState();
var motion120 = new CameraMotionState();
for (int i = 0; i < 30; i++) motion30.Advance(200f, 30f, true, 0.5f, true, 0.12f, 8f, 0.25f, 1f / 30f);
for (int i = 0; i < 120; i++) motion120.Advance(200f, 30f, true, 0.5f, true, 0.12f, 8f, 0.25f, 1f / 120f);
Near(motion30.DistanceMultiplier, motion120.DistanceMultiplier, "Motion damping is frame-rate independent");
Near(motion30.PitchDegrees, motion120.PitchDegrees, "Pitch damping is frame-rate independent");

var slowReturn = new TargetLookBlend();
slowReturn.Advance(true, 1f);
Near(slowReturn.Advance(false, 0.1f, 0.5f), 0.8f, "Configurable release keeps tracking pose while recentering the base");
Near(slowReturn.Advance(false, 0.2f, 0.5f), 0.4f, "Smooth return takes the requested time");
Near(slowReturn.Advance(true, 0.05f, 0.5f), 0.6f, "Reacquisition reverses a smooth return without clearing it");
Near(slowReturn.Advance(false, 0.1f, 0f), 0f, "Zero return time permits instant recentering");

var ace = PresetValues.For(CameraPreset.AceCombat);
var ace8 = PresetValues.For(CameraPreset.AceCombat8);
var wt = PresetValues.For(CameraPreset.WarThunder);
var plus = PresetValues.For(CameraPreset.NuclearOptionPlus);
Expect(ace.Centering && ace.Force >= 0.9f && !ace.LevelHorizon, true, "Ace Combat uses strong aircraft-relative centering");
Expect(ace.SpeedMotion && !ace.PitchFollow && ace8.PitchFollow, true, "Ace Combat 8 adds pitch anticipation to the Ace camera");
Expect(wt.LevelHorizon && !wt.Centering && wt.Force > 0f, true, "War Thunder has a level horizon without centering");
Expect(plus.Instruments == HudAttachment.Aircraft && !plus.SpeedMotion && !plus.PitchFollow && !plus.Centering, true, "Plus keeps additional arcade behavior restrained");
Expect(wt.Instruments == HudAttachment.Camera && ace.Instruments == HudAttachment.Camera, true, "Arcade presets keep instruments visible while looking away");
var actionNames = new[] { "FreeLook", "ToggleChase", "CockpitHold", "CameraRollToggle", "CenteringToggle", "WeaponCamera" };
var newIds = actionNames.Select(n => StableActionId.For("ArcadeCam::" + n)).ToArray();
Expect(newIds.Distinct().Count() == newIds.Length, true, "ArcadeCam input actions have unique stable ids");
Expect(newIds.Intersect(actionNames.Take(4).Select(n => StableActionId.For("ChaseCamPlus::" + n))).Any(), false, "ArcadeCam input ids do not collide with original ChaseCamPlus bindings");
// HUD placement must be independent of the native HMD's screen-fixed readouts.
Expect(HudLayoutMath.IsFlightInstrument(0), true, "Real HUD instruments participate in attachment");
Expect(HudLayoutMath.IsFlightInstrument(1), false, "Helmet duplicates are excluded from aircraft frame calculations");
Expect(HudLayoutMath.IsFlightInstrument(2), false, "Cockpit MFDs are excluded from flight HUD attachment");
Vector3 top = new Vector3(0f, 100f, 0f);
Vector3 left = new Vector3(-100f, 0f, 0f);
float leftBank = HudLayoutMath.BankDegrees(Vector3.left, Vector3.up);
float rightBank = HudLayoutMath.BankDegrees(Vector3.right, Vector3.down);
Near(leftBank, 90f, "A left bank uses a positive UI bank angle");
Near(rightBank, -90f, "A right bank uses a negative UI bank angle");
Near(HudLayoutMath.PlaceOffset(top, HudAttachment.Aircraft, leftBank).x, -100f, "Aircraft compass moves left in a left knife-edge bank");
Near(HudLayoutMath.PlaceOffset(top, HudAttachment.Aircraft, rightBank).x, 100f, "Aircraft compass moves right in a right knife-edge bank");
Near(HudLayoutMath.PlaceOffset(left, HudAttachment.Aircraft, leftBank).y, -100f, "Aircraft speed cluster banks with the plane instead of counter-rotating");
foreach (float bank in new[] { -180f, -90f, -45f, 0f, 45f, 90f, 180f })
{
    Vector3 fixedOffset = HudLayoutMath.PlaceOffset(new Vector3(-120f, 30f, 0f), HudAttachment.Camera, bank);
    Near(fixedOffset.x, -120f, "Camera speed/radar layout never moves with aircraft bank");
    Near(fixedOffset.y, 30f, "Camera layout stays upright through inverted flight");
}
Near(HudLayoutMath.BankDegrees(Vector3.forward, Vector3.up), 90f, "Looking along aircraft up falls back to projected wing direction");
Near(HudLayoutMath.BankDegrees(new Vector3(-0.5f, 0.5f, 0.7f), Vector3.right), 45f, "Off-axis bank uses projected up, not Euler z subtraction");
Vector3 invertedTop = HudLayoutMath.PlaceOffset(top, HudAttachment.Aircraft, 180f);
Near(invertedTop.y, -100f, "Aircraft instruments follow an inverted aircraft");

var centerReturn = new ViewReturnBlend();
centerReturn.Begin(0.4f);
Expect(centerReturn.Active, true, "Center View starts a return transition");
float firstReturn = centerReturn.Advance(0.01f);
Expect(firstReturn > 0f && firstReturn < 0.01f, true, "Center View starts near the previous rendered pose, not at the center");
Near(centerReturn.Advance(0.19f), 0.5f, "Half a return reaches the midpoint of the smooth turn");
Near(centerReturn.Advance(0.2f), 1f, "Center View reaches the forward chase destination");
Expect(centerReturn.Active, false, "Center return terminates without residual blend");
centerReturn.Begin(0.4f);
Near(centerReturn.Advance(-1f), 0f, "Invalid negative time cannot jump the center transition");
Near(centerReturn.Advance(0f), 0f, "Paused time holds the center transition");
centerReturn.Reset();
Expect(centerReturn.Active, false, "Leaving chase clears center transition state");
centerReturn.Begin(0f);
Near(centerReturn.Advance(0.01f), 1f, "Zero return setting preserves optional immediate centering");
var center30 = new ViewReturnBlend(); var center120 = new ViewReturnBlend();
center30.Begin(0.6f); center120.Begin(0.6f);
float a30 = 0f, a120 = 0f;
for (int i = 0; i < 6; i++) a30 = center30.Advance(1f / 30f);
for (int i = 0; i < 24; i++) a120 = center120.Advance(1f / 120f);
Near(a30, a120, "Center View timing is frame-rate independent");

var strongMotion = new CameraMotionState();
strongMotion.Advance(150f, 20f, true, 1f, true, 0.12f, 8f, 0f, 0.1f);
Near(strongMotion.PitchDegrees, 7.2f, "Normal pitch rates produce three times the old anticipation without raising the cap");
Near(strongMotion.DistanceMultiplier, 1.072f, "Ordinary speed produces more pullback within the original limit");
for (int i = 0; i < 12; i++) strongMotion.Advance(150.6f + i * 0.6f, 0f, true, 1f, false, 0f, 8f, 0f, 0.1f);
Expect(strongMotion.DistanceMultiplier > 1.17f, true, "Normal positive acceleration now creates visible motion at maximum strength");
Expect(strongMotion.DistanceMultiplier <= 1.30f, true, "Stronger gain preserves maximum travel");
var springMotion = new CameraMotionState();
springMotion.Advance(0f, 0f, false, 1f, true, 0.12f, 8f, 0.25f, 0.01f);
springMotion.Advance(0f, 20f, false, 1f, true, 0.12f, 8f, 0.25f, 0.01f);
float firstPitchStep = springMotion.PitchDegrees;
Expect(firstPitchStep > 0f && firstPitchStep < 0.03f, true, "A pitch-rate step eases in with near-zero starting velocity");
springMotion.Advance(0f, 20f, false, 1f, true, 0.12f, 8f, 0.25f, 0.01f);
Expect(springMotion.PitchDegrees - firstPitchStep > firstPitchStep, true, "The spring builds motion smoothly instead of instantly changing velocity");
for (int i = 0; i < 300; i++) springMotion.Advance(200f, i % 2 == 0 ? 200f : -200f, true, 1f, true, 1f, 8f, 0.25f, 1f / 120f);
Expect(Math.Abs(springMotion.PitchDegrees) <= 8f && springMotion.DistanceMultiplier <= 1.3f, true, "Noisy maximum inputs remain smooth and bounded");
// Picture-in-picture placement is clamped to the main camera viewport rather than
// assuming 16:9, full screen, or a particular HUD canvas scale.
var picture = TargetingScreenLayout.Calculate(1920f, 1080f, 100f, 25f, 24f, 1.8f);
Near(picture.Width, 460.8f, "Target screen width defaults to 24 percent of the viewport");
Near(picture.Height, 256f, "Target screen preserves the native feed aspect");
Near(picture.Left + picture.Width, 1908f, "Default panel sits against the right margin");
Expect(picture.Top > 150f && picture.Top < 300f, true, "Default panel leaves upper-right space for weapon selection");
foreach (var viewport in new[] { (1920f, 1080f), (1920f, 1200f), (1280f, 1024f), (3440f, 1440f), (640f, 480f) })
foreach (float x in new[] { -20f, 0f, 50f, 100f, 120f })
foreach (float y in new[] { -20f, 0f, 50f, 100f, 120f })
{
    var panel = TargetingScreenLayout.Calculate(viewport.Item1, viewport.Item2, x, y, 50f, 1.8f);
    Expect(panel.Left >= 0f && panel.Top >= 0f && panel.Left + panel.Width <= viewport.Item1 + 0.01f
        && panel.Top + panel.Height <= viewport.Item2 + 0.01f, true, "Movable target screen remains inside wide, tall and 16:10 viewports");
}
var portraitFeed = TargetingScreenLayout.Calculate(1920f, 1080f, 100f, 100f, 50f, 0.25f);
Expect(portraitFeed.Top + portraitFeed.Height <= 1080f && portraitFeed.Left + portraitFeed.Width <= 1920f, true, "An unusual tall feed is fitted without clipping");
Near(portraitFeed.Width / portraitFeed.Height, 0.25f, "Fitting an unusual feed does not stretch it");
var invalidAspect = TargetingScreenLayout.Calculate(1920f, 1080f, 0f, 0f, 24f, float.NaN);
Near(invalidAspect.Width / invalidAspect.Height, 1.8f, "An unavailable aspect uses the native default safely");
var rightBottom = TargetingScreenLayout.Calculate(1920f, 1080f, 100f, 100f, 24f, 1.8f);
Near(rightBottom.Top + rightBottom.Height, 1068f, "The bottom position slider respects the lower margin");
for (int bit = 0; bit < 9; bit++)
{
    // Disable each required condition in isolation, or enable landing mode.
    bool[] flags = { true, true, true, true, true, true, true, false, true };
    flags[bit] = !flags[bit];
    var unavailable = new TargetingScreenState(flags[0], flags[1], flags[2], flags[3], flags[4], flags[5], flags[6], flags[7], flags[8]);
    Expect(unavailable.Visible, false, "No stale feed is shown when an availability condition fails");
}
Expect(new TargetingScreenState(true, true, true, true, true, true, true, false, true).Visible, true,
    "A running native targeting feed appears on the local chase HUD");
var fittedImage = TargetingScreenLayout.FitImage(460.8f, 256f, 2f, 1.8f);
Near(fittedImage.Width / fittedImage.Height, 1.8f, "The border does not distort the actual video image");
Expect(fittedImage.Left >= 2f && fittedImage.Top >= 2f, true, "The image stays inside its frame padding");
var squareFrame = TargetingScreenLayout.FitImage(300f, 300f, 2f, 1.8f);
Near(squareFrame.Width / squareFrame.Height, 1.8f, "A square frame still displays an unstretched wide sensor image");
Expect(squareFrame.Top > 2f && squareFrame.Left >= 2f, true, "Unusual aspect ratios are centered inside the frame");
Near(FreeLookAngles.Wrap(181f), -179f, "Yaw continues across the old rear stop");
Near(FreeLookAngles.Wrap(-181f), 179f, "Yaw crosses the rear stop in the other direction");
Near(FreeLookAngles.Step(179f, -179f, 0.5f), -180f, "Rear seam smoothing follows the short path");
Near(FreeLookAngles.Step(-179f, 179f, 0.5f), -180f, "Reverse rear seam smoothing follows the short path");
Near(FreeLookAngles.Step(10f, 100f, 0f), 10f, "Zero blend holds yaw");
float circularPan = 0f;
bool continuous = true;
for (int i = 0; i < 720; i++)
{
    float nextPan = FreeLookAngles.Wrap(circularPan + 5f);
    continuous &= Math.Abs(FreeLookAngles.Wrap(nextPan - circularPan) - 5f) < 0.001f;
    circularPan = nextPan;
}
Expect(continuous, true, "Yaw remains unlimited through ten turns");
Near(circularPan, 0f, "Ten yaw turns return to the same orientation");
Near(FreeLookAngles.ClampTilt(1000f, 0f), 89.5f, "Downward pitch stops before inversion");
Near(FreeLookAngles.ClampTilt(-1000f, 0f), -89.5f, "Upward pitch stops before inversion");
Near(FreeLookAngles.ClampTilt(88f, 0f), 88f, "The old 85-degree ceiling is not restored");
Near(FreeLookAngles.ClampTilt(-88f, 0f), -88f, "The old 85-degree floor is not restored");
Near(FreeLookAngles.StepTilt(80f, 100f, 0.5f, 0f), 84.75f, "Pitch smoothing approaches the pole without wrapping");
Near(FreeLookAngles.StepTilt(89.5f, -89.5f, 0.5f, 0f), 0f, "Pitch reverses through center rather than through the pole");
Near(FreeLookAngles.StepTilt(100f, 120f, 0f, 0f), 89.5f, "A changed flight frame cannot leave smoothed pitch inverted");
float heldTilt = 0f;
for (int i = 0; i < 1000; i++) heldTilt = FreeLookAngles.ClampTilt(heldTilt + 10f, 0f);
Near(heldTilt, 89.5f, "Holding the stick at the pole does not accumulate hidden turns");
Near(FreeLookAngles.ClampTilt(heldTilt - 1f, 0f), 88.5f, "The first reverse input leaves the limit immediately");
foreach (float baseElevation in new[] { -80f, -30f, -5f, 0f, 5f, 30f, 80f })
{
    bool upright = true;
    for (int requested = -720; requested <= 720; requested++)
    {
        float tilt = FreeLookAngles.ClampTilt(requested, baseElevation);
        float elevation = baseElevation - tilt;
        upright &= Math.Abs(elevation) <= 89.5001f && Math.Cos(elevation * Math.PI / 180) > 0;
    }
    Expect(upright, true, "Native camera elevation is included in both pole limits");
}
Near(FreeLookAngles.Elevation(Vector3.forward, Vector3.up), 0f, "Level forward has zero elevation");
Near(FreeLookAngles.Elevation(Vector3.up, Vector3.up), 90f, "Vertical base elevation is detected");
Vector3 flatYaw = FreeLookAngles.YawAxis(Vector3.forward, Vector3.up, Vector3.up);
Near(flatYaw.y, 1f, "Level flight retains the normal yaw reference");
Vector3 verticalYaw = FreeLookAngles.YawAxis(Vector3.up, Vector3.back, Vector3.up);
Near(Vector3.Dot(verticalYaw, Vector3.up), 0f, "Vertical-flight pan uses screen up instead of turning into roll");
Near(verticalYaw.z, -1f, "Vertical-flight look remains steerable at the pole");
Near(FreeLookAngles.Elevation(Vector3.up, verticalYaw), 0f, "Vertical flight retains full local pitch travel");
Vector3 rolledYaw = FreeLookAngles.YawAxis(Vector3.up, Vector3.back, Vector3.back);
Near(rolledYaw.z, -1f, "Aircraft-relative vertical flight retains its own up reference");

var feedReadiness = new TargetingFeedReadiness();
Expect(feedReadiness.CanShow(10), false, "An unobserved feed cannot be displayed");
feedReadiness.Observe(1, 2, 10);
Expect(feedReadiness.CanShow(10), false, "A created texture is not proof of a rendered sensor frame");
feedReadiness.Rendered(99, 2, 10);
Expect(feedReadiness.CanShow(11), false, "An unrelated camera cannot validate the texture");
feedReadiness.Rendered(1, 99, 10);
Expect(feedReadiness.CanShow(11), false, "Rendering a different texture cannot validate this feed");
feedReadiness.Rendered(1, 2, 10);
Expect(feedReadiness.CanShow(10), true, "A completed sensor render may be displayed later in the same frame");
feedReadiness.Observe(1, 2, 11);
Expect(feedReadiness.CanShow(11), true, "The first complete native sensor frame can be displayed");
Expect(feedReadiness.CanShow(12), true, "An active sensor does not blink when its render rate differs from the game frame rate");
feedReadiness.Rendered(1, 2, 12);
Expect(feedReadiness.CanShow(13), true, "A resumed native camera supplies a fresh frame");
feedReadiness.Observe(3, 2, 13);
Expect(feedReadiness.CanShow(13), false, "Aircraft/camera replacement invalidates a reused texture");
feedReadiness.Rendered(1, 2, 13);
Expect(feedReadiness.CanShow(14), false, "The previous aircraft cannot validate a new camera");
feedReadiness.Rendered(3, 2, 13);
Expect(feedReadiness.CanShow(14), true, "A new camera becomes ready after its own render");
feedReadiness.Observe(3, 4, 14);
Expect(feedReadiness.CanShow(14), false, "A resized/replaced texture waits for new content");
feedReadiness.Rendered(3, 4, 14);
Expect(feedReadiness.CanShow(15), true, "The replacement texture becomes ready after rendering");
feedReadiness.Reset();
Expect(feedReadiness.CanShow(15), false, "Leaving chase or clearing a target resets feed readiness");
feedReadiness.Observe(3, 4, 15);
Expect(feedReadiness.CanShow(16), false, "Reactivating cannot display the previous session's texture");
feedReadiness.Rendered(3, 4, 14);
Expect(feedReadiness.CanShow(16), false, "A render from before activation is rejected");
feedReadiness.Rendered(3, 4, 16);
Expect(feedReadiness.CanShow(16), true, "OnGUI can use this frame after the native render callback");
Expect(feedReadiness.CanShow(19), true, "Several non-rendering game frames do not hide a still-active native sensor");
Expect(feedReadiness.CanShow(15), false, "A future render cannot validate an earlier frame");
feedReadiness.Reset();
Expect(feedReadiness.CanShow(19), false, "Stopping a camera or losing context invalidates its last image immediately");
var aceDelayInput = new CenteringInputState();
float aceDelay = PresetValues.For(CameraPreset.AceCombat).CenteringDelay;
Near(aceDelay, 0.55f, "Ace Combat uses a 55-percent delay slider value");
Near(PresetValues.For(CameraPreset.AceCombat8).CenteringDelay, 0.55f, "Ace Combat 8 uses the same delay");
Near(PresetValues.For(CameraPreset.NuclearOptionPlus).CenteringDelay, 0.05f, "Plus keeps its previous release delay");
Expect(aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, true, 1f, aceDelay, 0.01f) == CenteringAction.None,
    true, "Moving the stick does not center");
Expect(aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, aceDelay, 0.54f) == CenteringAction.None,
    true, "Ace centering waits through the first 0.54 seconds after release");
Expect(aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, false, 1f, aceDelay, 0.02f) == CenteringAction.Snap,
    true, "Full-force centering starts after the longer delay");
aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, true, 0.95f, aceDelay, 0.01f);
Expect(aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, false, 0.95f, aceDelay, 0.54f) == CenteringAction.None,
    true, "The actual Ace Combat preset strength also honors the delay");
Expect(aceDelayInput.Read(CenteringBehavior.OnInputRelease, true, false, false, 0.95f, aceDelay, 0.02f) == CenteringAction.Pull,
    true, "Ace Combat begins its strong pull only after the delay");
// Rebase the same view direction through level, banked, inverted and vertical
// native frames. Camera modes must share a view rather than share angle numbers.
Vector3 heldDirection = new Vector3(0.4f, 0.2f, 1f).normalized;
for (int bank = -180; bank <= 180; bank += 5)
{
    double radians = bank * Math.PI / 180;
    Vector3 rolledUp = new Vector3((float)Math.Sin(radians), (float)Math.Cos(radians), 0f);
    Vector2 rebased = FreeLookAngles.Rebase(heldDirection, Vector3.forward, rolledUp);
    Near(Vector3.Dot(FreeLookAngles.Direction(rebased.x, rebased.y, Vector3.forward, rolledUp), heldDirection),
        1f, "Changing roll reference preserves the held world viewing direction");
}
Vector3[] forwards = { Vector3.forward, Vector3.up, Vector3.back, new Vector3(0.3f, 0.8f, 0.5f).normalized };
Vector3[] ups = { Vector3.up, Vector3.back, Vector3.down, new Vector3(-0.2f, 0.4f, -0.8f).normalized };
for (int frameIndex = 0; frameIndex < forwards.Length; frameIndex++)
{
    Vector3 orbitUp = FreeLookAngles.YawAxis(forwards[frameIndex], ups[frameIndex], ups[frameIndex]);
    for (int yaw = -179; yaw <= 179; yaw += 30)
    {
        foreach (float tilt in new[] { -60f, -15f, 0f, 35f, 60f })
        {
            float clamped = FreeLookAngles.ClampTilt(tilt, FreeLookAngles.Elevation(forwards[frameIndex], orbitUp));
            Vector3 worldView = FreeLookAngles.Direction(yaw, clamped, forwards[frameIndex], orbitUp);
            Vector2 restored = FreeLookAngles.Rebase(worldView, forwards[frameIndex], orbitUp);
            Near(Vector3.Dot(FreeLookAngles.Direction(restored.x, restored.y, forwards[frameIndex], orbitUp), worldView),
                1f, "Pan/pitch roundtrip is stable in each camera reference frame");
        }
    }
}
Vector2 pending = FreeLookAngles.Rebase(new Vector3(-0.6f, 0.3f, 0.7f).normalized, Vector3.forward, Vector3.right);
Near(Vector3.Dot(FreeLookAngles.Direction(pending.x, pending.y, Vector3.forward, Vector3.right),
    new Vector3(-0.6f, 0.3f, 0.7f).normalized), 1f, "Pending smoothed look input also survives a banked mode switch");
Vector2 cap = FreeLookAngles.Rebase(Vector3.up, Vector3.forward, Vector3.up);
Near(cap.y, -89.5f, "Rebasing directly onto a pole keeps the anti-inversion limit");

HudLayoutStamp LayoutStamp(int canvas = 1, float width = 1920f, float height = 1080f, float scale = 1f,
    HudAttachment left = HudAttachment.Camera, HudAttachment right = HudAttachment.Camera,
    HudAttachment compass = HudAttachment.Camera, HudAttachment waterline = HudAttachment.Aircraft) =>
    new(canvas, new Vector2(width, height), new Vector3(scale, scale, scale), left, right, compass, waterline);
HudLayoutStamp stableLayout = LayoutStamp();
Expect(stableLayout.Matches(LayoutStamp()), true, "Ordinary flight/camera frames retain the existing HUD attachments");
Expect(stableLayout.Matches(LayoutStamp(width: 1920.001f, scale: 1.00001f)), true, "Subpixel layout noise does not trigger reparenting");
Expect(stableLayout.Matches(LayoutStamp(canvas: 2)), false, "A new HUD canvas recaptures original layout");
Expect(stableLayout.Matches(LayoutStamp(width: 1600f)), false, "A changed resolution width rebuilds layout");
Expect(stableLayout.Matches(LayoutStamp(height: 1200f)), false, "A changed resolution height rebuilds layout");
Expect(stableLayout.Matches(LayoutStamp(scale: 1.2f)), false, "A changed HUD scale rebuilds layout");
Expect(stableLayout.Matches(LayoutStamp(left: HudAttachment.Aircraft)), false, "Changing left attachment restores its native layout");
Expect(stableLayout.Matches(LayoutStamp(right: HudAttachment.Hidden)), false, "Hiding the right group rebuilds visibility immediately");
Expect(stableLayout.Matches(LayoutStamp(compass: HudAttachment.Aircraft)), false, "Changing compass attachment rebuilds layout");
Expect(stableLayout.Matches(LayoutStamp(waterline: HudAttachment.Hidden)), false, "Changing waterline attachment rebuilds layout");
var weaponView = new WeaponCameraState();
Expect(weaponView.Displaying, false, "Weapon view begins inactive");
Expect(weaponView.Input(true, true, false, true, null), false, "Holding weapon view without a live shot does not select another unit");
Expect(weaponView.Input(true, false, false, true, 11), true, "Hold can start when a newly fired player weapon becomes available");
Expect(weaponView.TrackedToken == 11, true, "Activation selects the latest live shot");
Expect(weaponView.Input(true, false, false, true, 22), false, "A new shot does not interrupt the currently followed weapon");
Expect(weaponView.TrackedToken == 11, true, "The followed weapon stays fixed during the session");
weaponView.Detonated(0.75f);
Expect(weaponView.Phase == WeaponViewPhase.Impact, true, "A confirmed detonation enters the brief impact view");
weaponView.Advance(0.5f, 0.75f, 0.35f);
Expect(weaponView.Phase == WeaponViewPhase.Impact, true, "Impact view lasts the selected duration");
weaponView.Lost();
Expect(weaponView.Phase == WeaponViewPhase.Impact, true, "Weapon destruction cannot prematurely cancel the already-confirmed impact view");
weaponView.Detonated(0.75f);
Near(weaponView.PhaseTime, 0.5f, "Duplicate detonation notification does not restart the orbit");
weaponView.Input(false, false, false, true, 22);
Expect(weaponView.Phase == WeaponViewPhase.Returning, true, "Releasing hold always cancels even the impact view");
Near(weaponView.ReturnBlend(0.35f), 0f, "Return begins at the saved rendered pose");
weaponView.Detonated(0.75f);
Expect(weaponView.Phase == WeaponViewPhase.Returning, true, "A late detonation cannot override a deliberate release");
weaponView.Advance(0.175f, 0.75f, 0.35f);
Near(weaponView.ReturnBlend(0.35f), 0.5f, "Return has a smooth midpoint");
weaponView.Advance(0.175f, 0.75f, 0.35f);
Expect(weaponView.Displaying, false, "Return completes and drops the override");
Expect(weaponView.Input(false, false, false, true, 22), false, "Unheld input cannot reactivate the view");
weaponView.Input(true, true, false, true, 22);
weaponView.Lost();
Expect(weaponView.Phase == WeaponViewPhase.Returning, true, "A vanished weapon returns without inventing an explosion");
weaponView.Advance(0.35f, 0.75f, 0.35f);
Expect(weaponView.Input(true, false, false, true, 33), false, "Holding through automatic return does not jump onto another launched weapon");
weaponView.Input(false, false, false, true, 33);
Expect(weaponView.Input(true, true, false, true, 33), true, "A fresh hold can follow the next shot");
weaponView.Input(true, false, false, false, 33);
Expect(weaponView.Displaying, false, "Menu, death, camera switch or aircraft loss cancels immediately");
Expect(weaponView.Input(true, false, false, true, 33), false, "Closing a menu while held cannot restart weapon view");
weaponView.Input(false, false, false, true, 33);
Expect(weaponView.Input(true, true, false, true, 33), true, "Fresh input works after context recovery");
weaponView.Input(true, false, true, true, 33);
Expect(weaponView.Displaying, false, "Changing hold/toggle mode mid-press cancels instead of latching");
Expect(weaponView.Input(true, false, true, true, 33), false, "Held input stays blocked after changing mode");
weaponView.Input(false, false, true, true, 33);
Expect(weaponView.Input(true, true, true, true, 33), true, "A fresh toggle press starts tracking");
weaponView.Input(false, false, true, true, 44);
Expect(weaponView.Phase == WeaponViewPhase.Following && weaponView.TrackedToken == 33, true, "Toggle continues through button release and newer shots");
weaponView.Input(true, true, true, true, 44);
Expect(weaponView.Phase == WeaponViewPhase.Returning, true, "The second toggle press returns");
weaponView.Input(false, false, true, true, 44);
Expect(weaponView.Input(true, true, true, true, 44), true, "A deliberate new press can interrupt return and select the latest shot");
Expect(weaponView.TrackedToken == 44, true, "Reactivation uses the latest token");
weaponView.Detonated(0f);
Expect(weaponView.Phase == WeaponViewPhase.Returning, true, "Disabling impact duration skips the destruction orbit");
weaponView.Advance(0f, 0f, 0f);
Expect(weaponView.Displaying, false, "Zero return duration gives an immediate aircraft return");
weaponView.Input(false, false, true, true, 44);
weaponView.Input(true, true, true, true, 44);
weaponView.Detonated(0.75f);
weaponView.Advance(-1f, 0.75f, 0.35f);
Near(weaponView.PhaseTime, 0f, "Negative frame time cannot advance or rewind impact view");
weaponView.Advance(1.2f, 0.75f, 0.35f);
Expect(weaponView.Displaying, false, "A long frame finishes impact and return with elapsed-time carryover");
weaponView.Input(false, false, true, true, null);
Expect(weaponView.Input(true, true, true, true, null), false, "Toggle with no live owned weapon is a harmless no-op");

var replicatedWeaponView = new WeaponCameraState();
replicatedWeaponView.Input(true, true, false, true, 7);
replicatedWeaponView.Lost(0.04f, 0.08f);
Expect(replicatedWeaponView.Phase == WeaponViewPhase.Following, true, "Disabled replication can briefly wait for the confirmed detonation callback");
replicatedWeaponView.Detonated(0.75f);
Expect(replicatedWeaponView.Phase == WeaponViewPhase.Impact, true, "A detonation arriving after disabled replication still gets its impact view");
replicatedWeaponView.Cancel();
replicatedWeaponView.Input(false, false, false, true, 8);
replicatedWeaponView.Input(true, true, false, true, 8);
replicatedWeaponView.Lost(0.04f, 0.08f);
replicatedWeaponView.Lost(0.04f, 0.08f);
Expect(replicatedWeaponView.Phase == WeaponViewPhase.Returning, true, "Missing detonation confirmation returns after a bounded grace period");
var weaponMotion = new WeaponCameraMotion();
weaponMotion.Advance(Vector3.forward, Vector3.right, 500f, 1f, 1f, true, 0.016f);
Near(weaponMotion.Side, -0.5f, "Cinematic view begins at a rear-left quarter angle");
Near(weaponMotion.Distance, 13.2f, "Cinematic distance accounts for weapon size and speed");
Expect(weaponMotion.Height > 0f, true, "Weapon camera is raised slightly above the trajectory");
weaponMotion.Advance(Vector3.left, Vector3.right, 500f, 1f, 1f, true, 0.016f);
Expect(weaponMotion.Side > -0.5f && weaponMotion.Side < 0.5f, true, "A turn eases the camera toward the other side instead of snapping");
for (int i = 0; i < 120; i++) weaponMotion.Advance(Vector3.left, Vector3.right, 500f, 1f, 1f, true, 1f / 60f);
Expect(weaponMotion.Side > 0.45f, true, "The cinematic camera can settle on the opposite flank");
float oldSide = weaponMotion.Side;
weaponMotion.Advance(Vector3.forward, Vector3.right, 500f, 1f, 1f, true, 0f);
Near(weaponMotion.Side, oldSide, "Zero elapsed time cannot move the side offset");
weaponMotion.Advance(Vector3.forward, Vector3.right, 10000f, 10000f, 100f, true, 1f);
Expect(weaponMotion.Distance <= 144f && Math.Abs(weaponMotion.Side) <= 0.5f, true, "Very large/fast weapons retain bounded camera offsets");
weaponMotion.Advance(Vector3.forward, Vector3.right, 500f, 1f, 1f, false, 0.016f);
Near(weaponMotion.Side, 0f, "Simple mode uses a direct rear view");
Near(weaponMotion.Distance, 12f, "Simple mode omits cinematic speed pullback");
foreach (Vector3 verticalForward in new[] { Vector3.up, Vector3.down, new Vector3(0.001f, 1f, 0.001f).normalized })
{
    Vector3 right = WeaponCameraMotion.Right(verticalForward, Vector3.right);
    Near(Vector3.Dot(right, verticalForward), 0f, "Vertical missile/bomb framing keeps a valid perpendicular right axis");
    Expect(right.sqrMagnitude > 0.99f, true, "Vertical flight cannot collapse the trailing camera frame");
    Expect(Vector3.Dot(right, Vector3.right) > 0.99f, true, "Near-vertical flight transports the previous side axis without a sudden azimuth swing");
}
var lowWeaponMotion = new WeaponCameraMotion();
var highWeaponMotion = new WeaponCameraMotion();
foreach (var weaponMotionSample in new[] { lowWeaponMotion, highWeaponMotion })
{
    weaponMotionSample.Advance(Vector3.forward, Vector3.right, 500f, 1f, 1f, true, 0.01f);
    weaponMotionSample.Advance(Vector3.left, Vector3.right, 500f, 1f, 1f, true, 0.01f);
}
for (int i = 0; i < 30; i++) lowWeaponMotion.Advance(Vector3.left, Vector3.right, 500f, 1f, 1f, true, 1f / 30f);
for (int i = 0; i < 120; i++) highWeaponMotion.Advance(Vector3.left, Vector3.right, 500f, 1f, 1f, true, 1f / 120f);
Near(lowWeaponMotion.Side, highWeaponMotion.Side, "Cinematic side transition timing is consistent across frame rates");
// Distant re-entry must not leave a velocity-dependent lag behind the projectile.
// Keep the real managed Unity vector maths while exercising flight-like paths.
var weaponFollow = new WeaponCameraFollow();
Vector3 boom = new Vector3(-6f, 3f, -12f);
foreach (float fps in new[] { 15f, 30f, 60f, 120f })
foreach (float speed in new[] { 0f, 100f, 500f, 1500f, 3000f })
{
    weaponFollow.Reset();
    Vector3 point = new Vector3(8000f, 2000f, -12000f);
    Vector3 cameraPosition = point + weaponFollow.Advance(boom, 1f / fps);
    Expect((cameraPosition - point - boom).sqrMagnitude < 0.00001f, true,
        "Distant activation immediately acquires the configured rear-quarter view");
    for (int frame = 0; frame < (int)(fps * 3f); frame++)
    {
        point += Vector3.forward * (speed / fps);
        // Simulate a floating-origin shift during tracking. Neither boom nor
        // acquisition depends on world coordinates or a cached aircraft pose.
        if (frame == (int)fps) point -= new Vector3(8192f, 1024f, -12288f);
        cameraPosition = point + weaponFollow.Advance(boom, 1f / fps);
    }
    Expect((cameraPosition - point - boom).sqrMagnitude < 0.00001f, true,
        "Weapon translation and origin shifts cannot accumulate tracking lag at any tested speed/frame rate");
    weaponFollow.Reset();
    point += new Vector3(40000f, -500f, 20000f);
    Vector3 newBoom = new Vector3(8f, 4f, -16f);
    cameraPosition = point + weaponFollow.Advance(newBoom, 0f);
    Expect((cameraPosition - point - newBoom).sqrMagnitude < 0.00001f, true,
        "Reactivation snaps to the live weapon even after it has travelled kilometres");
}
weaponFollow.Reset();
weaponFollow.Advance(boom, 0f);
Vector3 changedBoom = new Vector3(6f, 3f, -12f);
Vector3 easedBoom = weaponFollow.Advance(changedBoom, 0.05f);
Expect(easedBoom.x > boom.x && easedBoom.x < changedBoom.x, true,
    "Turns still ease the relative camera boom instead of snapping every frame");
Vector3 stoppedBoom = weaponFollow.Advance(changedBoom, -1f);
Expect((stoppedBoom - easedBoom).sqrMagnitude < 0.00001f, true,
    "Negative elapsed time cannot move a settled weapon boom");
var slowFollow = new WeaponCameraFollow();
var fastFollow = new WeaponCameraFollow();
slowFollow.Advance(boom, 0f); fastFollow.Advance(boom, 0f);
for (int i = 0; i < 30; i++) slowFollow.Advance(changedBoom, 1f / 30f);
for (int i = 0; i < 120; i++) fastFollow.Advance(changedBoom, 1f / 120f);
Expect((slowFollow.Offset - fastFollow.Offset).sqrMagnitude < 0.00001f, true,
    "Relative boom smoothing is consistent across frame rates");
var emptyWeaponView = new WeaponCameraState();
for (int i = 0; i < 5; i++)
{
    Expect(emptyWeaponView.Input(true, true, false, true, null), false,
        "Repeated pre-launch presses cannot start a weapon-camera pose");
    Expect(emptyWeaponView.Displaying, false,
        "The first and subsequent empty presses leave the aircraft view active");
    emptyWeaponView.Input(false, false, false, true, null);
}
Expect(emptyWeaponView.Input(true, true, false, true, 5), true,
    "First launch still works after pressing weapon view before firing");

// Screen-space layout must survive a world-origin translation followed by Unity
// restoring the Canvas root. Exercise the production mapper with real Unity maths.
foreach (var size in new[] { new Vector2(1920f, 1200f), new Vector2(1920f, 1080f), new Vector2(1280f, 1024f) })
foreach (float uiScale in new[] { 0.5f, 1f, 1.5f, 2f })
foreach (float pivot in new[] { 0f, 0.5f, 1f })
{
    Rect pixels = new Rect(0f, 0f, size.x, size.y);
    Rect localRect = new Rect(-size.x * pivot / uiScale, -size.y * pivot / uiScale, size.x / uiScale, size.y / uiScale);
    Vector3 canvasRoot = new Vector3(size.x * pivot, size.y * pivot, 0f);
    foreach (var screenPoint in new[] { size * 0.5f, Vector2.zero, size, new Vector2(size.x * 0.2f, size.y * 0.65f) })
    {
        Expect(HudCanvasMath.TryScreenToLocal(screenPoint, pixels, localRect, out Vector3 localPoint), true,
            "Every tested screen size, HUD scale and canvas pivot maps into local UI coordinates");
        Vector3 expectedScreen = new Vector3(screenPoint.x, screenPoint.y, 0f);
        Expect((canvasRoot + localPoint * uiScale - expectedScreen).sqrMagnitude < 0.001f, true,
            "Canvas-local placement preserves the requested pixel position");
        foreach (Vector3 originShift in new[] { new Vector3(1024f, 0f, 0f), new Vector3(-2048f, 1024f, 0f), new Vector3(0f, -1024f, 0f) })
        {
            // The old world-position assignment compensates for the temporary
            // root translation. After root reset that becomes an unwanted jump.
            Vector3 shiftedRoot = canvasRoot - originShift;
            Vector3 oldLocal = (expectedScreen - shiftedRoot) / uiScale;
            Vector3 oldRendered = canvasRoot + oldLocal * uiScale;
            Expect((oldRendered - expectedScreen).sqrMagnitude > 100000f, true,
                "World placement has a demonstrable one-frame jump if the canvas root is reset after an origin shift");
            Expect((canvasRoot + localPoint * uiScale - expectedScreen).sqrMagnitude < 0.001f, true,
                "Canvas-local presentation remains correct after root reset without adding an origin compensation");
        }
    }
}
Expect(HudCanvasMath.TryScreenToLocal(new Vector2(700f, 500f), new Rect(0f, 0f, 1920f, 1200f),
    new Rect(-960f, -600f, 1920f, 1200f), out Vector3 subViewPoint), true,
    "A camera viewport center can be mapped into a larger HUD canvas");
Near(subViewPoint.x, -260f, "Sub-viewport positioning retains the requested horizontal screen coordinate");
Near(subViewPoint.y, -100f, "Sub-viewport positioning retains the requested vertical screen coordinate");
Expect(HudCanvasMath.TryScreenToLocal(new Vector2(1100f, 700f), new Rect(100f, 100f, 2000f, 1200f),
    new Rect(-500f, -300f, 1000f, 600f), out Vector3 shiftedPixelPoint), true,
    "Canvas pixel rectangles with a nonzero display origin are supported");
Near(shiftedPixelPoint.sqrMagnitude, 0f, "The center of a nonzero pixel rectangle maps to the local center");
Expect(HudCanvasMath.TryScreenToLocal(Vector2.zero, new Rect(0f, 0f, 0f, 1200f), new Rect(-960f, -600f, 1920f, 1200f), out _), false,
    "A minimized/zero-width viewport cannot generate invalid placement");
Expect(HudCanvasMath.TryScreenToLocal(new Vector2(float.NaN, 0f), new Rect(0f, 0f, 1920f, 1200f), new Rect(-960f, -600f, 1920f, 1200f), out _), false,
    "Invalid aircraft projection cannot poison the canvas position");
Expect(HudCanvasMath.TryScreenToLocal(new Vector2(float.PositiveInfinity, 0f), new Rect(0f, 0f, 1920f, 1200f), new Rect(-960f, -600f, 1920f, 1200f), out _), false,
    "Infinite near-plane projection is rejected");
Vector3 scaledNeutral = HudCanvasMath.RelativeOffset(new Vector3(-150f, 25f, 4f), new Vector3(2f, 3f, 1f), new Vector3(4f, 6f, 2f));
Near(scaledNeutral.x, -75f, "Neutral instrument offset uses scale relative to the canvas");
Near(scaledNeutral.y, 12.5f, "Nonuniform reference/canvas scale is preserved");
Near(scaledNeutral.z, 2f, "Neutral depth is expressed in canvas units too");
Expect(HudCanvasMath.HasPositionDrift(scaledNeutral, scaledNeutral), false, "A stable local pose does not produce a drift report");
Expect(HudCanvasMath.HasPositionDrift(scaledNeutral, scaledNeutral + new Vector3(0.005f, 0f, 0f)), false,
    "Sub-pixel numerical noise is below the presentation drift threshold");
Expect(HudCanvasMath.HasPositionDrift(scaledNeutral, scaledNeutral + Vector3.right), true,
    "A late layout shift of a widget is detected before rendering");
Expect(HudCanvasMath.HasPositionDrift(scaledNeutral, new Vector3(float.NaN, 0f, 0f)), true,
    "A nonfinite late-written widget position is detected for correction");

// An explosion next to a guidance target is not sufficient to claim target damage.
var impactEvidence = new WeaponImpactEvidence();
impactEvidence.Detonate(10f, true);
Expect(impactEvidence.Confirmed, false, "A nearby target without damage cannot start wreckage following");
impactEvidence.Damage(10.03f, 5f);
Expect(impactEvidence.Confirmed, true, "Damage on the next physics frame confirms the impact subject");
impactEvidence.Detonate(12f, false);
Expect(impactEvidence.Confirmed, true, "A duplicate RPC cannot replace confirmed impact evidence");
impactEvidence.Reset();
impactEvidence.Damage(9f, 50f);
impactEvidence.Detonate(10f, true);
Expect(impactEvidence.Confirmed, false, "Old battle damage cannot turn a miss into a target-follow view");
impactEvidence.Damage(10.5f, 10f);
Expect(impactEvidence.Confirmed, false, "Damage outside the bounded confirmation window is ignored");
impactEvidence.Reset();
impactEvidence.Damage(9.95f, 5f);
impactEvidence.Detonate(10f, true);
Expect(impactEvidence.Confirmed, true, "Closely reordered damage and detonation replication can still confirm a hit");
impactEvidence.Reset();
impactEvidence.Detonate(10f, false);
impactEvidence.Damage(10.02f, 100f);
Expect(impactEvidence.Confirmed, false, "Damage to a distant guidance target cannot select unrelated wreckage");
impactEvidence.Reset();
impactEvidence.Detonate(10f, true);
impactEvidence.Damage(10.02f, 0f);
Expect(impactEvidence.Confirmed, false, "A damage notification with no pierce or blast damage is insufficient");
impactEvidence.Reset();
Expect(impactEvidence.Confirmed, false, "A new target or camera session clears the previous hit evidence");

foreach (bool toggle in new[] { false, true })
{
    var aftermath = new WeaponCameraState();
    aftermath.Input(true, true, toggle, true, 17);
    aftermath.Detonated(2f);
    aftermath.Advance(1.5f, 2f, 0.35f);
    Expect(aftermath.Phase == WeaponViewPhase.Impact, true, "A two-second aftermath stays active in hold and toggle modes");
    aftermath.Input(!toggle, false, toggle, true, 88);
    Expect(aftermath.TrackedToken == 17, true, "New launches cannot steal an aftermath view");
    aftermath.Input(toggle, toggle, toggle, true, 88);
    Expect(aftermath.Phase == WeaponViewPhase.Returning, true, "Release or a second toggle press cancels wreckage following");
    aftermath.Advance(0.35f, 2f, 0.35f);
    Expect(aftermath.Displaying, false, "Aftermath cancellation completes the usual live-aircraft return");
}
var timedAftermath = new WeaponCameraState();
timedAftermath.Input(true, true, false, true, 17);
timedAftermath.Detonated(2f);
timedAftermath.Advance(2.4f, 2f, 0.35f);
Expect(timedAftermath.Displaying, false, "A long frame consumes the wreckage duration and return without latching");
Expect(timedAftermath.Input(true, false, false, true, 99), false,
    "Holding through an automatic aftermath return still requires a fresh press");

foreach (float speed in new[] { 0f, 300f, 1200f })
foreach (int fps in new[] { 15, 30, 120 })
{
    var stationaryImpact = new WeaponImpactMotion();
    var movingImpact = new WeaponImpactMotion();
    var entryOffset = new Vector3(-15f, 6f, -25f);
    stationaryImpact.Begin(entryOffset); movingImpact.Begin(entryOffset);
    for (int frame = 1; frame <= fps * 2; frame++)
    {
        float elapsed = frame / (float)fps;
        Vector3 subject = new Vector3(speed * elapsed, 40f - elapsed * elapsed * 5f, 500f);
        Vector3 stationary = stationaryImpact.Advance(Vector3.zero, 10f, elapsed, 1f / fps);
        Vector3 moving = movingImpact.Advance(subject, 10f, elapsed, 1f / fps);
        Expect((moving - subject - stationary).sqrMagnitude < 0.00001f, true,
            "The wreckage's full translation is retained across speeds and frame rates");
        Expect(stationary.magnitude >= 13.49f && stationary.magnitude < 50f, true,
            "Aftermath framing stays outside the subject and within a modest viewing distance");
    }
}
var closeImpact = new WeaponImpactMotion();
closeImpact.Begin(Vector3.zero);
Expect(closeImpact.Advance(Vector3.zero, 20f, 0f, 0f).magnitude >= 26.99f, true,
    "A close detonation cannot leave the aftermath camera inside a large target");
Expect(WeaponImpactMotion.Shake(0.1f, 0f) == Vector3.zero, true, "Zero strength disables impact shake");
Expect(WeaponImpactMotion.Shake(0f, 1f) == Vector3.zero, true, "Impact shake begins without an angular jump");
Expect(WeaponImpactMotion.Shake(0.4f, 1f) == Vector3.zero, true, "Explosion shake ends before the longer wreckage view");
Expect(WeaponImpactMotion.Shake(2f, 1f) == Vector3.zero, true, "Shake cannot persist through the return");
float shakePeak = 0f, oldSettingPeak = 0f;
for (int frame = 1; frame < 400; frame++)
{
    float elapsed = frame / 1000f;
    Vector3 shake = WeaponImpactMotion.Shake(elapsed, 0.65f);
    shakePeak = Math.Max(shakePeak, Math.Abs(shake.x));
    oldSettingPeak = Math.Max(oldSettingPeak, Math.Abs(WeaponImpactMotion.Shake(elapsed, 0.35f).x));
    Expect(Math.Abs(shake.x) <= 1.561f && Math.Abs(shake.y) <= 1.041f && Math.Abs(shake.z) <= 0.456f,
        true, "Stronger explosion shake remains bounded on every axis");
    Expect((WeaponImpactMotion.Shake(elapsed, 1f) * 0.65f - shake).sqrMagnitude < 0.000001f,
        true, "Absolute-time shake is deterministic and its strength scales without accumulating rotation");
}
Expect(shakePeak > 1f, true, "The requested explosion kick has a clearly stronger peak than the previous quarter degree");
Expect(oldSettingPeak > 0.45f, true, "An existing 0.35 strength setting also receives a stronger explosion effect");

var flightCanvas = new TestCanvas { Enabled = true };
var nestedCanvas = new TestCanvas { Enabled = true };
var alreadyHiddenCanvas = new TestCanvas();
var destroyedCanvas = new TestCanvas { Enabled = true };
var menuCanvas = new TestCanvas { Enabled = true };
var hudVisibility = new VisibilityOverride<TestCanvas>(node => !node.Destroyed,
    node => node.Enabled, (node, enabled) => {
        if (node.Destroyed) throw new Exception("Destroyed UI must not be written during restoration");
        node.Enabled = enabled;
    });
hudVisibility.Hide(flightCanvas); hudVisibility.Hide(nestedCanvas);
hudVisibility.Hide(alreadyHiddenCanvas); hudVisibility.Hide(destroyedCanvas);
Expect(flightCanvas.Enabled || nestedCanvas.Enabled, false, "Flight and independent nested HUD canvases are both suppressed");
Expect(menuCanvas.Enabled, true, "Unrelated UI is outside the visibility snapshot");
for (int frame = 0; frame < 4; frame++)
{
    hudVisibility.Hide(flightCanvas); hudVisibility.Hide(nestedCanvas);
    hudVisibility.Reapply();
    Expect(flightCanvas.Enabled || nestedCanvas.Enabled, false,
        "Repeated suppression retains hidden visibility throughout the weapon session");
}
flightCanvas.Enabled = true;
hudVisibility.Reapply();
Expect(flightCanvas.Enabled, false, "A late native re-enable is suppressed again before overlay drawing");
destroyedCanvas.Destroyed = true;
hudVisibility.Restore();
Expect(flightCanvas.Enabled && nestedCanvas.Enabled, true, "Leaving weapon view restores the original visible canvases");
Expect(alreadyHiddenCanvas.Enabled, false, "A HUD canvas that was already hidden stays hidden after return");
hudVisibility.Restore();
Expect(flightCanvas.Enabled, true, "Repeated cancellation/restoration is safe");
flightCanvas.Enabled = false;
hudVisibility.Hide(flightCanvas); hudVisibility.Restore();
Expect(flightCanvas.Enabled, false, "A new weapon session takes a fresh snapshot of current visibility");

Console.WriteLine($"PASS: {checks} camera, motion, input, HUD and targeting-screen layout/availability checks.");

sealed class TestCanvas
{
    internal bool Enabled, Destroyed;
}
