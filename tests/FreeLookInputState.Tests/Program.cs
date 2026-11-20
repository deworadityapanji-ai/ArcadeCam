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

var dyn = new DynamicCameraState();
dyn.Advance(2f, 2f, 2f, true, 1f, 1f, 3f, 0.3f, 0.1f);
Expect(DynamicCameraState.InputSmoothing > 0f, true, "Dynamic camera smooths its rate inputs");
Near(dyn.Side, 0f, "Dynamic camera ignores rates inside the deadzone");
Near(dyn.Lift, 0f, "Dynamic camera deadzone covers pitch");
dyn.Reset();
for (int i = 0; i < 400; i++) dyn.Advance(0f, 0f, -200f, true, 1f, 1f, 3f, 0.3f, 1f / 120f);
Expect(dyn.Side < -0.06f, true, "Rolling right slides the camera left");
Expect(dyn.RollLag > 5f, true, "Rolling right makes the camera roll trail to the left");
Expect(dyn.Weight > 0.99f, true, "Enabled dynamic camera reaches full weight");
dyn.Reset();
for (int i = 0; i < 400; i++) dyn.Advance(-60f, 0f, 0f, true, 1f, 0f, 3f, 0.3f, 1f / 120f);
Expect(dyn.Lift > 0.1f, true, "Pulling up raises the camera so the aircraft sits low in the frame");
Near(dyn.RollLag, 0f, "Zero roll lag keeps roll locked");
dyn.Reset();
for (int i = 0; i < 400; i++) dyn.Advance(-500f, 500f, -500f, true, 3f, 1f, 0f, 0.3f, 1f / 120f);
Expect((float)Math.Sqrt(dyn.Side * dyn.Side + dyn.Lift * dyn.Lift) <= DynamicCameraState.TotalMax + 0.001f, true, "Dynamic travel stays capped at maximum strength");
for (int i = 0; i < 400; i++) dyn.Advance(-500f, 500f, -500f, false, 2f, 1f, 0f, 0.3f, 1f / 120f);
Expect(dyn.Weight < 0.01f, true, "Suspending the dynamic camera fades it out");
var dyn30 = new DynamicCameraState();
var dyn120 = new DynamicCameraState();
for (int i = 0; i < 30; i++) dyn30.Advance(-20f, 10f, -90f, true, 1f, 0.3f, 3f, 0.3f, 1f / 30f);
for (int i = 0; i < 120; i++) dyn120.Advance(-20f, 10f, -90f, true, 1f, 0.3f, 3f, 0.3f, 1f / 120f);
Near(dyn30.Side, dyn120.Side, "Dynamic camera is frame-rate independent");
Near(dyn30.Lift, dyn120.Lift, "Dynamic lift is frame-rate independent");
var jitter = new DynamicCameraState();
float jitterMin = float.MaxValue, jitterMax = float.MinValue;
for (int i = 0; i < 600; i++)
{
    // A sustained roll whose physics-step rate alternates +-40 deg/s, as fly-by-wire does.
    jitter.Advance(0f, 0f, i % 2 == 0 ? -160f : -240f, true, 2f, 0.3f, 3f, 0.7f, 1f / 120f);
    if (i > 400) { jitterMin = Math.Min(jitterMin, jitter.Side); jitterMax = Math.Max(jitterMax, jitter.Side); }
}
Expect(jitterMax - jitterMin < 0.002f, true, "A jittery sustained roll gives a steady camera offset");

// Kill cam scoring
Expect(KillCamScore.Eligible(KillKind.Aircraft, false) && KillCamScore.Eligible(KillKind.Ship, false), true, "Aircraft and ship kills qualify");
Expect(KillCamScore.Eligible(KillKind.Vehicle, false) || KillCamScore.Eligible(KillKind.Building, false), false, "Ground kills need the ground option");
Expect(KillCamScore.Eligible(KillKind.Vehicle, true), true, "The ground option lets vehicles qualify");
Expect(KillCamScore.Eligible(KillKind.Missile, true), false, "Shooting down a missile never qualifies");
int missileKill = KillCamScore.Score(KillKind.Aircraft, false, 1f, 5000f, false);
int gunKill = KillCamScore.Score(KillKind.Aircraft, true, 1f, 800f, false);
Expect(gunKill > missileKill + 30, true, "Gun kills score much higher than missile kills");
int carrier = KillCamScore.Score(KillKind.Ship, false, 20f, 6000f, false);
Expect(carrier >= KillCamScore.Guaranteed, true, "A carrier-sized kill is guaranteed");
Near(KillCamScore.Chance(carrier, KillCamFrequency.Rare), 1f, "Guaranteed kills play even on Rare");
Near(KillCamScore.Chance(missileKill, KillCamFrequency.Always), 1f, "Always plays every eligible kill");
Near(KillCamScore.Chance(gunKill, KillCamFrequency.Off), 0f, "Off never plays");
Expect(KillCamScore.Chance(missileKill, KillCamFrequency.Rare) < 0.2f, true, "An ordinary missile kill is rare on Rare");
Expect(KillCamScore.Chance(gunKill, KillCamFrequency.Rare) > KillCamScore.Chance(missileKill, KillCamFrequency.Rare) * 1.8f, true, "A gun kill is far likelier");
Expect(KillCamScore.Cooldown(KillCamFrequency.Rare, missileKill) > KillCamScore.Cooldown(KillCamFrequency.Often, missileKill), true, "Rarer settings wait longer between kill cams");
Expect(KillCamScore.Score(KillKind.Aircraft, false, 1f, 5000f, true) > missileKill, true, "Multi-kills score higher");
Expect(KillCamScore.CutsToWreck(KillCamView.Auto, carrier) && KillCamScore.CutsToWreck(KillCamView.Auto, gunKill), true, "Auto cuts to the wreck for big targets and close gun kills");
Expect(KillCamScore.CutsToWreck(KillCamView.Auto, missileKill), false, "Auto watches ordinary kills from the aircraft");
Expect(KillCamScore.CutsToWreck(KillCamView.FromAircraft, carrier), false, "FromAircraft never cuts");
Expect(KillCamScore.CutsToWreck(KillCamView.CutToWreck, missileKill), true, "CutToWreck always cuts");

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
var actionNames = new[] { "FreeLook", "ToggleChase", "CockpitHold", "CameraRollToggle", "CenteringToggle", "WeaponCamera" };
var newIds = actionNames.Select(n => StableActionId.For("ArcadeCam::" + n)).ToArray();
Expect(newIds.Distinct().Count() == newIds.Length, true, "ArcadeCam input actions have unique stable ids");
Expect(newIds.Intersect(actionNames.Take(4).Select(n => StableActionId.For("ChaseCamPlus::" + n))).Any(), false, "ArcadeCam input ids do not collide with original ChaseCamPlus bindings");
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

// Orbit follows horizontal travel and keeps a defined heading through vertical
// flight, stops, reversals, pauses and a fresh aircraft/camera session.
var orbitPreset = PresetValues.For(CameraPreset.Orbit);
Expect(orbitPreset.LevelHorizon && orbitPreset.LevelStyle == LevelCameraStyle.Orbit
    && !orbitPreset.SpeedMotion && !orbitPreset.PitchFollow && !orbitPreset.Centering, true,
    "Orbit preset selects upright steady framing without added motion or automatic centering");
Expect(PresetValues.For(CameraPreset.WarThunder).LevelStyle == LevelCameraStyle.Cinematic, true,
    "The existing WarThunder preset retains its cinematic level-horizon style");
var orbitCourse = new OrbitCourse();
Vector3 orbitAhead = orbitCourse.Advance(new Vector3(0f, 30f, 120f), Vector3.forward, 0.02f);
Near(orbitAhead.y, 0f, "Climbing does not pitch the orbit course");
Near(orbitAhead.z, 1f, "Initial orbit heading follows horizontal travel");
for (int frame = 0; frame < 180; frame++)
{
    float pitch = frame * (float)Math.PI / 90f;
    Vector3 nose = new(0f, (float)Math.Sin(pitch), (float)Math.Cos(pitch));
    Vector3 heading = orbitCourse.Advance(new Vector3(0f, frame - 90f, 120f), nose, 1f / 60f);
    Near(heading.y, 0f, "Loop pitch/vertical velocity cannot tilt orbit heading");
    Near(heading.z, 1f, "An aircraft rotating about a stable course stays centered");
}
Vector3 beforeVertical = orbitCourse.Advance(Vector3.right * 150f, Vector3.right, 0.1f);
Vector3 duringVertical = orbitCourse.Advance(Vector3.up * 150f, Vector3.up, 0.02f);
Near((duringVertical - beforeVertical).sqrMagnitude, 0f, "Vertical flight retains the last defined horizontal heading");
Vector3 pausedOrbit = orbitCourse.Advance(Vector3.back * 150f, Vector3.back, 0f);
Near((pausedOrbit - duringVertical).sqrMagnitude, 0f, "Zero elapsed time cannot move an initialized orbit heading");
Vector3 negativeOrbit = orbitCourse.Advance(Vector3.back * 150f, Vector3.back, -1f);
Near((negativeOrbit - duringVertical).sqrMagnitude, 0f, "Negative elapsed time cannot reverse smoothing");
orbitCourse.Reset();
Near(orbitCourse.Advance(Vector3.zero, Vector3.left, 0.02f).x, -1f, "A stopped aircraft uses its horizontal nose direction");
orbitCourse.Reset();
Vector3 poleStart = orbitCourse.Advance(Vector3.up * 200f, Vector3.up, 0f);
Near(poleStart.sqrMagnitude, 1f, "Entering directly vertical starts with a valid heading");
Near(poleStart.y, 0f, "The vertical-entry fallback is horizontal");
orbitCourse.Reset();
orbitCourse.Advance(Vector3.forward * 100f, Vector3.forward, 0.02f);
Vector3 turnStart = orbitCourse.Advance(Vector3.right * 100f, Vector3.right, 0.02f);
Expect(turnStart.x > 0f && turnStart.z > 0f, true, "Orbit turns toward a new course without an instant heading snap");
for (int frame = 0; frame < 180; frame++) orbitCourse.Advance(Vector3.right * 100f, Vector3.right, 1f / 60f);
Near(orbitCourse.Advance(Vector3.right * 100f, Vector3.right, 0.02f).x, 1f, "Orbit settles onto the new travel direction");
orbitCourse.Reset();
Near(orbitCourse.Advance(Vector3.back * 100f, Vector3.forward, 0.02f).z, -1f,
    "A new camera session discards the previous aircraft's course");
foreach (float radius in new[] { 0f, 1f, 8f, 35f })
{
    float normalDistance = OrbitCourse.Distance(radius, 0f);
    Near(normalDistance, 2f * (1f + radius), "Orbit distance scales with aircraft radius");
    Expect(OrbitCourse.Distance(radius, -0.75f) <= normalDistance, true, "Zooming in shortens the orbit arm");
    Expect(OrbitCourse.Distance(radius, 1f) >= normalDistance, true, "Zooming out lengthens the orbit arm");
    Expect(OrbitCourse.Distance(radius, -100f) >= 2f, true, "Extreme close zoom retains a finite offset");
}
orbitCourse.Reset();
orbitCourse.Advance(Vector3.zero, Vector3.forward, 0.1f);
Near(orbitCourse.Advance(Vector3.zero, Vector3.back, 0.05f).z, -1f,
    "An exact 180-degree reversal cannot leave orbit stuck on its old heading");
// Reproduce the reported ordering: UI laid out first, scene roots translated
// during weapon rendering, overlay drawn afterward. Exercise the production
// snapshot policy with fake canvas roots rather than fabricated Unity callbacks.
var chatRoot = new TestOverlay { Position = new Vector3(960f, 540f, 0f), Overlay = true };
var worldRoot = new TestOverlay { Position = new Vector3(20f, 3f, 40f), Overlay = false };
var originGuard = new OriginShiftSnapshot<TestOverlay>(
    node => !node.Destroyed && node.Overlay, node => node.Position,
    (node, position) => { node.Position = position; node.Writes++; });
foreach (Vector3 shift in new[] {
    new Vector3(1024f, 0f, -128f), new Vector3(-4096f, 2048f, 512f),
    new Vector3(65536f, -32768f, 131072f), new Vector3(-65536f, 32768f, -131072f) })
{
    Vector3 originalChat = chatRoot.Position, originalWorld = worldRoot.Position;
    int writesBefore = chatRoot.Writes;
    originGuard.Capture(chatRoot); originGuard.Capture(chatRoot); originGuard.Capture(worldRoot);
    // Datum.originPosition moves with the scene roots. This is the recorded
    // post-shift delta, rather than the opposite camera-position shift amount.
    chatRoot.Position += shift; worldRoot.Position += shift;
    Expect(originGuard.Restore(shift) == 1, true, "A shared overlay root is corrected once per shift");
    Near((chatRoot.Position - originalChat).sqrMagnitude, 0f, "Chat remains at its pre-layout screen position");
    Near((worldRoot.Position - (originalWorld + shift)).sqrMagnitude, 0f, "World-space UI follows the world normally");
    Expect(chatRoot.Writes == writesBefore + 1, true, "Duplicate chat/feed references do not double-compensate a shared canvas");
    Expect(originGuard.Restore(shift) == 0, true, "A completed shift cannot restore a stale snapshot again");
}
Vector3 smallShift = new(1024f, 0f, 128f);
originGuard.Capture(chatRoot);
Expect(originGuard.Restore(smallShift) == 0, true, "Unity already re-anchored this canvas; no second compensation");
Vector3 movedByLayout = chatRoot.Position + Vector3.right * 30f;
originGuard.Capture(chatRoot); chatRoot.Position = movedByLayout;
Expect(originGuard.Restore(smallShift) == 0 && chatRoot.Position == movedByLayout, true,
    "Independent UI layout changes are preserved");
originGuard.Capture(chatRoot); chatRoot.Position += smallShift; chatRoot.Destroyed = true;
Expect(originGuard.Restore(smallShift) == 0, true, "Destroyed chat roots are skipped during scene changes");
chatRoot.Destroyed = false;
originGuard.Capture(chatRoot);
Expect(originGuard.Restore(Vector3.zero) == 0, true, "A native threshold/editor no-op leaves the UI untouched");
chatRoot.Position += smallShift;
Expect(originGuard.Restore(smallShift) == 0, true, "A no-op clears its snapshot instead of leaking it into a future shift");
originGuard.Capture(chatRoot); chatRoot.Position += smallShift; chatRoot.Overlay = false;
Expect(originGuard.Restore(smallShift) == 0, true, "A canvas switched to world-space is not pinned to the screen");
chatRoot.Overlay = true;
originGuard.Capture(chatRoot);
Expect(originGuard.Restore(new Vector3(float.NaN, 0f, 0f)) == 0, true, "Invalid origin deltas cannot corrupt UI positions");
foreach (float fov in new[] { 40f, 60f, 90f, 110f })
foreach (float depth in new[] { 10f, 50f, 200f })
{
    Vector3 offset = new(3f, 0f, -depth);
    Vector3 framed = AimViewFraming.LiftOffset(offset, Vector3.forward, Vector3.up, fov);
    Near(framed.x, offset.x, "WT aim framing preserves lateral camera composition");
    Near(framed.z, offset.z, "WT aim framing preserves camera depth/zoom");
    float screenFraction = framed.y / depth / (2f * (float)Math.Tan(fov * Math.PI / 360f));
    Near(screenFraction, 0.16f, "An Orbit camera keeps the aircraft below the WT aim marker across FoV/zoom");
    Vector3 alreadyLow = new(3f, framed.y + 20f, -depth);
    Near((AimViewFraming.LiftOffset(alreadyLow, Vector3.forward, Vector3.up, fov) - alreadyLow).sqrMagnitude, 0f,
        "WT framing preserves an existing lower-aircraft cinematic composition");
}
// A held WT aim direction must not follow any later aircraft/base-rig rotation.
// The frame accepts no aircraft-up vector and separates heading from horizon leveling.
var mouseFrame = new MouseAimFrame();
Vector3 heldAim = new Vector3(0.35f, 0.2f, 1f).normalized;
mouseFrame.Advance(heldAim, heldAim, 8f, 1f / 60f);
for (int angle = 0; angle <= 720; angle++)
{
    double radians = angle * Math.PI / 180f;
    Vector3 changingBase = new((float)Math.Sin(radians), (float)Math.Sin(radians * 0.5), (float)Math.Cos(radians));
    mouseFrame.Advance(heldAim, changingBase, 8f, 1f / 60f);
    Near((mouseFrame.Forward - heldAim).sqrMagnitude, 0f, "A held world heading ignores later airframe/base-rig rotations");
    Near(mouseFrame.Forward.sqrMagnitude, 1f, "Held aim frame stays normalized");
    Near(Vector3.Dot(mouseFrame.Forward, mouseFrame.Up), 0f, "Held aim view remains orthogonal");
    Expect(Vector3.Dot(mouseFrame.Up, Vector3.up) > 0.95f, true, "Aircraft bank cannot roll the normal WT horizon");
    Near(mouseFrame.ReferenceUp.y, 1f, "WT free look uses world-up yaw away from the poles");
}
Vector3 preservedHeading = mouseFrame.Forward;
Vector3 preservedUp = mouseFrame.Up;
mouseFrame.Advance(Vector3.left, Vector3.back, 8f, 0f);
Near((mouseFrame.Forward - preservedHeading).sqrMagnitude, 0f, "Paused WT frame does not change heading");
Near((mouseFrame.Up - preservedUp).sqrMagnitude, 0f, "Paused WT frame does not change its horizon");
mouseFrame.Advance(new Vector3(float.NaN, 0f, 1f), Vector3.back, 8f, 0.02f);
Near((mouseFrame.Forward - preservedHeading).sqrMagnitude, 0f, "Invalid aim data cannot steer the camera");
mouseFrame.Reset();
mouseFrame.Advance(Vector3.forward, Vector3.forward, 8f, 0f);
mouseFrame.Advance(Vector3.back, Vector3.zero, 8f, 1f / 60f);
Expect(mouseFrame.Forward.x > 0f && mouseFrame.Forward.z < 1f, true, "An antipodal aim change starts a defined smooth turn");
for (int i = 0; i < 240; i++) mouseFrame.Advance(Vector3.back, Vector3.zero, 8f, 1f / 60f);
Near((mouseFrame.Forward - Vector3.back).sqrMagnitude, 0f, "Direction smoothing settles on a new world heading");
mouseFrame.Reset();
mouseFrame.Advance(Vector3.forward, Vector3.forward, 16f, 0f);
for (int angle = 1; angle <= 720; angle++)
{
    double radians = angle * Math.PI / 180f;
    Vector3 loopAim = new(0f, (float)Math.Sin(radians), (float)Math.Cos(radians));
    Vector3 previousUp = mouseFrame.Up;
    mouseFrame.Advance(loopAim, Vector3.right, 16f, 1f / 120f);
    Near(mouseFrame.Forward.sqrMagnitude, 1f, "WT loop heading remains normalized through both poles");
    Near(mouseFrame.Up.sqrMagnitude, 1f, "WT loop horizon remains normalized through both poles");
    Near(Vector3.Dot(mouseFrame.Forward, mouseFrame.Up), 0f, "WT loop frame remains nondegenerate");
    Expect(Vector3.Dot(previousUp, mouseFrame.Up) > 0.5f, true, "Pole transitions level without a single-frame horizon flip");
}
mouseFrame.Reset();
mouseFrame.Advance(Vector3.up, Vector3.up, 8f, 0f);
Near(Vector3.Dot(mouseFrame.Forward, mouseFrame.Up), 0f, "Entering WT camera directly vertical has a valid up vector");
Near(mouseFrame.Up.sqrMagnitude, 1f, "Vertical entry produces a normalized camera frame");
Console.WriteLine($"PASS: {checks} camera, orbit, world-aim frame, input and overlay-origin checks.");

sealed class TestCanvas
{
    internal bool Enabled, Destroyed;
}

sealed class TestOverlay
{
    internal Vector3 Position;
    internal bool Overlay, Destroyed;
    internal int Writes;
}
