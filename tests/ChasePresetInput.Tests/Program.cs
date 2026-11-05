using System.Reflection;
using System.Reflection.Emit;
using ArcadeCam;
using ArcadeCam.Helpers;
using ArcadeCam.Patches;
using HarmonyLib;
using UnityEngine;

// This checks the installed game's actual CheckInput IL, not a reconstruction.
string gameDir = args.Length > 0 ? args[0] : @"C:\Games\Nuclear Option";
string managed = Path.Combine(gameDir, "NuclearOption_Data", "Managed");
AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
{
    string name = new AssemblyName(request.Name).Name + ".dll";
    foreach (string folder in new[] { managed, Path.Combine(gameDir, "BepInEx", "core") })
    {
        string path = Path.Combine(folder, name);
        if (File.Exists(path)) return Assembly.LoadFrom(path);
    }
    return null;
};
int checks = 0;
void Check(bool result, string description)
{
    checks++;
    if (!result) throw new Exception(description);
}
for (int digit = 0; digit <= 9; digit++)
{
    var key = (KeyCode)((int)KeyCode.Keypad0 + digit);
    Check(!ChasePresetInput.Read(key, true, true), "A weapon-camera press wins over each hardcoded numbered view, including Numpad 7");
    Check(ChasePresetInput.Read(key, true, false), "Unclaimed number-pad shortcuts retain normal native behavior");
    Check(!ChasePresetInput.Read(key, false, true), "Holding before launch cannot synthesize a new number-pad press");
}
Check(ChasePresetInput.Read(KeyCode.H, true, true), "The editor H shortcut remains available");
Check(ChasePresetInput.Read(KeyCode.KeypadEnter, true, true), "Non-preset keys remain unchanged");
Check(!ChasePresetInput.Read(KeyCode.H, false, false), "No physical press never produces a shortcut");
var native = AccessTools.Method(typeof(CameraChaseState), nameof(CameraChaseState.CheckInput));
var original = PatchProcessor.GetOriginalInstructions(native).ToList();
var getKey = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) });
var wrapper = AccessTools.Method(typeof(ChasePresetInputPatches), nameof(ChasePresetInputPatches.GetKeyDown));
Check(original.Count(code => code.Calls(getKey)) == 11, "Installed native CheckInput has the expected ten views and editor key");
var rewritten = ChasePresetInputPatches.Transpiler(original).ToList();
Check(rewritten.Count == original.Count, "The rewrite preserves the native instruction count");
Check(!rewritten.Any(code => code.Calls(getKey)), "No raw keyboard read bypasses weapon-camera priority in native CheckInput");
Check(rewritten.Count(code => code.Calls(wrapper)) == 11, "The production patch replaces all native key reads");
for (int i = 0; i < original.Count; i++)
{
    Check(original[i].labels.SequenceEqual(rewritten[i].labels), "Native branch labels survive input wrapping");
    Check(original[i].blocks.SequenceEqual(rewritten[i].blocks), "Native exception blocks survive input wrapping");
    if (!original[i].Calls(getKey))
        Check(original[i].opcode == rewritten[i].opcode && Equals(original[i].operand, rewritten[i].operand),
            "All other native operations remain intact");
}
Check(Plugin.Logger.Warnings == 0, "The real game's IL passes patch validation");
var incompatible = new[] { new CodeInstruction(OpCodes.Ret) };
var fallback = ChasePresetInputPatches.Transpiler(incompatible).ToList();
Check(fallback.Count == 1 && fallback[0].opcode == OpCodes.Ret && Plugin.Logger.Warnings == 1,
    "An unexpected future native method remains untouched with a warning");
// Validate the installed native surface used for impact observation, without
// substituting a made-up hit callback or changing the game's damage execution.
var detonation = AccessTools.GetDeclaredMethods(typeof(Missile))
    .Single(method => method.Name.StartsWith("UserCode_RpcDetonate_", StringComparison.Ordinal));
var detonationArgs = detonation.GetParameters();
Check(detonationArgs.Length == 7 && detonationArgs[0].ParameterType == typeof(Unit)
    && detonationArgs[1].ParameterType == typeof(bool) && detonationArgs[2].ParameterType == typeof(Vector3)
    && detonationArgs[3].ParameterType == typeof(bool) && detonationArgs[4].ParameterType == typeof(bool)
    && detonationArgs[5].ParameterType == typeof(bool) && detonationArgs[6].ParameterType == typeof(Vector3),
    "The real missile detonation callback still matches the observer");
Check(AccessTools.Field(typeof(Missile), "target")?.FieldType == typeof(Unit),
    "The missile's own native target can be watched before damage/detonation replication");
Check(typeof(UnitPart).GetEvent("onApplyDamage")?.EventHandlerType == typeof(Action<UnitPart.OnApplyDamage>),
    "Damage confirmation uses the installed game's actual part-damage event");
var damageInvoke = AccessTools.Method(typeof(UnitPart), nameof(UnitPart.InvokeDamage));
Check(PatchProcessor.GetOriginalInstructions(damageInvoke).Any(code => code.operand is MethodInfo method
    && method.Name == "Invoke" && method.DeclaringType == typeof(Action<UnitPart.OnApplyDamage>)),
    "Native damage notification really invokes the subscribed event");
var applyDamage = AccessTools.Method(typeof(UnitPart), nameof(UnitPart.ApplyDamage));
Check(PatchProcessor.GetOriginalInstructions(applyDamage).Any(code => code.Calls(damageInvoke)),
    "Native part damage reaches the notification used by hit confirmation");
Check(AccessTools.Method(typeof(UnitPart), nameof(UnitPart.IsDetached)) != null
    && AccessTools.Method(typeof(Unit), nameof(Unit.GetAllParts)) != null,
    "Surviving and detached pieces can be followed using native ownership references");
// Inspect the actual built plugin's lifecycle methods too. The reported bug was
// restoring overlay HUD visibility at SRP endFrame, before overlay drawing.
string pluginPath = args.Length > 1 ? args[1] : Path.GetFullPath(Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
    "ChaseCamPlus", "bin", "Debug", "net472", "ArcadeCam.dll"));
var pluginAssembly = Assembly.LoadFrom(pluginPath);
var weaponType = pluginAssembly.GetType("ArcadeCam.Helpers.WeaponCamera", true);
var restoreHud = AccessTools.Method(weaponType, "RestoreHud");
foreach (string boundary in new[] { "EndFrame", "EndRender", "BeforeCameraUpdate" })
{
    var instructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, boundary)).ToList();
    Check(!instructions.Any(code => code.Calls(restoreHud)),
        "The actual plugin must not restore overlay HUD at the camera/context boundary: " + boundary);
}
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, "Cancel"))
    .Any(code => code.Calls(restoreHud)), "A real weapon-view cancellation restores saved HUD visibility");
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, "Prepare"))
    .Any(code => code.Calls(AccessTools.Method(weaponType, "UpdateHudVisibility"))),
    "The prepared weapon session suppresses the HUD before camera or overlay rendering");
var beginRender = PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, "BeginRender")).ToList();
var impactMotionType = pluginAssembly.GetType("ArcadeCam.Helpers.WeaponImpactMotion", true);
Check(beginRender.Any(code => code.Calls(AccessTools.Method(impactMotionType, "Shake"))),
    "The actual rendered weapon pose includes the explosion shake");
Check(!beginRender.Any(code => code.operand is MethodInfo method && method.Name == "get_Confirmed"),
    "Explosion shake does not wait for target-damage confirmation or require a target hit");
var hudType = pluginAssembly.GetType("ArcadeCam.Helpers.WeaponCameraHud", true);
Check(AccessTools.GetDeclaredMethods(hudType).SelectMany(method => PatchProcessor.GetOriginalInstructions(method))
    .All(code => !(code.operand is MethodInfo method && method.DeclaringType == typeof(GameObject)
        && method.Name == nameof(GameObject.SetActive))),
    "HUD suppression never deactivates gameplay/input GameObjects");
// Exercise the actual GLOC methods shipped with the installed game. The only
// permitted edit is its presentation camera-mode read, including the inverse
// condition which clears effects when leaving cockpit/chase.
var modeField = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraMode));
var glocMode = AccessTools.Method(typeof(GlocView), nameof(GlocView.EffectiveCameraMode));
var glocTargets = GLOCPatches.TargetMethods().ToList();
Check(glocTargets.Count == 2 && glocTargets.All(method => method != null),
    "Both native GLOC presentation and camera-switch methods exist");
foreach (var method in glocTargets)
{
    var before = PatchProcessor.GetOriginalInstructions(method).ToList();
    var after = GLOCPatches.Transpiler(before, method).ToList();
    Check(before.Count(code => code.opcode == OpCodes.Ldsfld && Equals(code.operand, modeField)) == 1,
        method.Name + " has exactly the validated native presentation guard");
    Check(after.Count == before.Count + 1 && after.Count(code => code.Calls(glocMode)) == 1,
        method.Name + " substitutes only one owner-aware presentation check");
    int cursor = 0;
    foreach (var instruction in before)
    {
        var rewrittenInstruction = after[cursor++];
        if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, modeField))
        {
            Check(rewrittenInstruction.opcode == OpCodes.Ldarg_0,
                "The owner is loaded on every branch into the presentation check");
            Check(after[cursor].Calls(glocMode) && after[cursor].labels.Count == 0 && after[cursor].blocks.Count == 0,
                "The replacement call has the correct stack argument and no bypassing labels");
            cursor++;
        }
        else
        {
            Check(instruction.opcode == rewrittenInstruction.opcode && Equals(instruction.operand, rewrittenInstruction.operand),
                "Native physiology, blackout triggers, audio, recovery and other operations remain unchanged");
        }
        Check(instruction.labels.SequenceEqual(rewrittenInstruction.labels), "All GLOC branch destinations are preserved");
        Check(instruction.blocks.SequenceEqual(rewrittenInstruction.blocks), "All GLOC exception boundaries are preserved");
    }
}
int warningsBeforeGlocFallback = Plugin.Logger.Warnings;
foreach (var unexpected in new[] {
    new[] { new CodeInstruction(OpCodes.Ret) },
    new[] { new CodeInstruction(OpCodes.Ldsfld, modeField), new CodeInstruction(OpCodes.Pop),
        new CodeInstruction(OpCodes.Ldsfld, modeField), new CodeInstruction(OpCodes.Ret) }
})
{
    var unchanged = GLOCPatches.Transpiler(unexpected, glocTargets[0]).ToList();
    Check(unchanged.SequenceEqual(unexpected), "An unexpected GLOC layout is left intact");
}
Check(Plugin.Logger.Warnings == warningsBeforeGlocFallback + 2,
    "Missing or additional GLOC camera checks produce diagnostics");
// Verify the built plugin includes this exact patch and never spoofs the global
// camera mode. These checks inspect the production assembly, not just linked tests.
var productionGlocPatch = pluginAssembly.GetType("ArcadeCam.Patches.GLOCPatches", true);
Check(productionGlocPatch.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 1,
    "Production GLOC patch is registered with Harmony PatchAll");
var productionGlocView = pluginAssembly.GetType("ArcadeCam.Helpers.GlocView", true);
var eligibility = PatchProcessor.GetOriginalInstructions(AccessTools.Method(productionGlocView, "EffectiveCameraMode")).ToList();
Check(!eligibility.Any(code => code.opcode == OpCodes.Stsfld && Equals(code.operand, modeField)),
    "GLOC presentation never modifies the game's actual camera mode");
Check(eligibility.Any(code => code.operand is MethodInfo method && method.Name == nameof(GameManager.GetLocalAircraft)),
    "External GLOC presentation checks local-player aircraft ownership");
Check(eligibility.Any(code => code.operand is MethodInfo method && method.Name == nameof(GLOC.GetComponent)
    && method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(Pilot) })),
    "External GLOC presentation checks the source pilot's ownership");
var orbitType = pluginAssembly.GetType("ArcadeCam.Helpers.OrbitCamera", true);
var orbitPose = AccessTools.Method(orbitType, "ReadPose");
var orbitInstructions = PatchProcessor.GetOriginalInstructions(orbitPose).ToList();
Check(!orbitInstructions.Any(code => code.opcode == OpCodes.Stsfld && Equals(code.operand, modeField)),
    "Orbit styling retains the real chase camera mode used by flight input and G-LOC");
Check(!orbitInstructions.Any(code => code.Calls(AccessTools.Method(typeof(CameraStateManager), nameof(CameraStateManager.SwitchState)))),
    "Orbit cannot switch into an unmodified native state and lose ArcadeCam features");
var levelCameraType = pluginAssembly.GetType("ArcadeCam.Helpers.CinematicCamera", true);
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(levelCameraType, "Apply")).Any(code => code.Calls(orbitPose)),
    "The production level-horizon pipeline integrates the orbit rig");
var motionType = pluginAssembly.GetType("ArcadeCam.Helpers.ChaseCameraMotion", true);
var motionInstructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(motionType, "Apply")).ToList();
int orbitGuard = motionInstructions.FindIndex(code => code.Calls(AccessTools.PropertyGetter(levelCameraType, "UsesOrbit")));
int extraMotion = motionInstructions.FindIndex(code => code.operand is MethodInfo method && method.Name == "Advance"
    && method.DeclaringType.Name == "CameraMotionState");
Check(orbitGuard >= 0 && extraMotion > orbitGuard,
    "Orbit's no-added-motion guard precedes speed/pitch motion updates");
// Inspect the installed native chat/root-shift surface and actual production
// hook. These checks do not simulate Unity canvas batching or network messages.
Check(AccessTools.Field(typeof(FloatingOrigin), "threshold")?.FieldType == typeof(float),
    "Chat protection reads the installed origin threshold rather than a guessed constant");
Check(AccessTools.Field(typeof(MessageUI), "messageText") != null
    && AccessTools.Field(typeof(MessageUI), "killFeedText") != null
    && AccessTools.Field(typeof(MessageUI), "messageBackground")?.FieldType == typeof(GameObject),
    "Native chat/feed display roots used by the guard still exist");
var nativeShift = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(FloatingOrigin), nameof(FloatingOrigin.OriginShift))).ToList();
Check(nativeShift.Any(code => code.Calls(AccessTools.Method(typeof(Datum), nameof(Datum.AfterOriginShift)))),
    "Native root movement records the origin delta before the protection finalizer runs");
var chatPatch = pluginAssembly.GetType("ArcadeCam.Patches.ChatOverlayOriginPatches", true);
var chatPrefix = AccessTools.Method(chatPatch, "Prefix");
var chatFinalizer = AccessTools.Method(chatPatch, "Finalizer");
Check(chatPrefix.ReturnType == typeof(void), "Chat protection cannot skip native origin shifts");
Check(chatFinalizer.GetCustomAttributes(typeof(HarmonyFinalizer), false).Length == 1,
    "Chat cleanup runs through a Harmony finalizer on interrupted paths");
var sentinelError = new InvalidOperationException("Native origin error sentinel");
Check(ReferenceEquals(chatFinalizer.Invoke(null, new object[] { sentinelError, null }), sentinelError),
    "Chat protection preserves an original native exception");
Check(chatFinalizer.Invoke(null, new object[] { null, null }) == null,
    "A skipped/no-op snapshot does not manufacture an error");
var chatOverlayType = pluginAssembly.GetType("ArcadeCam.Helpers.ChatOverlayOrigin", true);
var chatMethods = pluginAssembly.GetTypes().Where(type => type == chatOverlayType
    || type.DeclaringType == chatOverlayType).SelectMany(type => AccessTools.GetDeclaredMethods(type));
var chatInstructions = chatMethods.SelectMany(method => PatchProcessor.GetOriginalInstructions(method)).ToList();
Check(!chatInstructions.Any(code => code.operand is MethodInfo method && (
    (method.DeclaringType == typeof(GameObject) && method.Name == nameof(GameObject.SetActive))
    || method.Name == "SendChatMessage" || method.Name == "GameMessage"
    || method.Name == "set_enabled" || method.Name == "set_text")),
    "Chat stabilization never hides UI, changes chat contents, sends messages or toggles input objects");
// Native sound culling/propagation must use the audio viewpoint, while other
// source transforms and all non-audio operations retain their original IL.
var audioType = pluginAssembly.GetType("ArcadeCam.Helpers.WeaponCameraAudio", true);
var audioPosition = AccessTools.Method(audioType, "ListenerPosition");
var audioPositionPatch = pluginAssembly.GetType("ArcadeCam.Patches.WeaponExplosionAudioPosition", true);
var audioTargets = (IEnumerable<MethodBase>)AccessTools.Method(audioPositionPatch, "TargetMethods").Invoke(null, null);
foreach (var method in audioTargets)
{
    var before = PatchProcessor.GetOriginalInstructions(method).ToList();
    var after = ((IEnumerable<CodeInstruction>)AccessTools.Method(audioPositionPatch, "Transpiler")
        .Invoke(null, new object[] { before, method })).ToList();
    var getPosition = AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));
    int expected = method.DeclaringType == typeof(ExplosionAudio) ? 2 : 1;
    Check(before.Count(code => code.Calls(getPosition)) == expected
        && after.Count(code => code.Calls(audioPosition)) == expected,
        "Native explosion culling and propagation have the validated audio-position wrappers");
    Check(after.Count == before.Count, "Audio position wrappers preserve native instruction count");
    for (int i = 0; i < before.Count; i++)
    {
        Check(before[i].labels.SequenceEqual(after[i].labels) && before[i].blocks.SequenceEqual(after[i].blocks),
            "Audio wrappers preserve native control-flow metadata");
        Check(before[i].Calls(getPosition) ? after[i].Calls(audioPosition)
            : before[i].opcode == after[i].opcode && Equals(before[i].operand, after[i].operand),
            "Only native position getters in audio methods are adapted");
    }
}
var velocityPatch = pluginAssembly.GetType("ArcadeCam.Patches.WeaponSonicAudioVelocity", true);
var sonicMethod = (MethodBase)AccessTools.Method(velocityPatch, "TargetMethod").Invoke(null, null);
Check(sonicMethod != null, "The installed sonic-boom audio method exists");
var sonicBefore = PatchProcessor.GetOriginalInstructions(sonicMethod).ToList();
var sonicAfter = ((IEnumerable<CodeInstruction>)AccessTools.Method(velocityPatch, "Transpiler")
    .Invoke(null, new object[] { sonicBefore })).ToList();
var velocityField = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraVelocity));
var audioVelocity = AccessTools.Method(audioType, "ListenerVelocity");
Check(sonicBefore.Count(code => code.opcode == OpCodes.Ldfld && Equals(code.operand, velocityField)) == 1
    && sonicAfter.Count(code => code.Calls(audioVelocity)) == 1, "Sonic-boom audio uses the view's relative velocity");
for (int i = 0; i < sonicBefore.Count; i++)
{
    bool velocityRead = sonicBefore[i].opcode == OpCodes.Ldfld && Equals(sonicBefore[i].operand, velocityField);
    Check(velocityRead ? sonicAfter[i].Calls(audioVelocity)
        : sonicBefore[i].opcode == sonicAfter[i].opcode && Equals(sonicBefore[i].operand, sonicAfter[i].operand),
        "Other native supersonic-sound operations remain intact");
    Check(sonicBefore[i].labels.SequenceEqual(sonicAfter[i].labels) && sonicBefore[i].blocks.SequenceEqual(sonicAfter[i].blocks),
        "Sonic-boom audio control flow remains intact");
}
var stopAudio = AccessTools.Method(audioType, "Restore");
foreach (string boundary in new[] { "EndRender", "EndFrame", "BeforeCameraUpdate" })
    Check(!PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, boundary)).Any(code => code.Calls(stopAudio)),
        "The audio viewpoint survives render-only camera restoration: " + boundary);
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(weaponType, "Cancel")).Any(code => code.Calls(stopAudio)),
    "Cancelling weapon view restores native listener/camera sound states");
var updaterType = pluginAssembly.GetType("ArcadeCam.Helpers.ChaseHudUpdater", true);
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(updaterType, "LateUpdate"))
    .Any(code => code.Calls(AccessTools.Method(audioType, "Tick"))), "Weapon audio gets the finalized view every LateUpdate");
var stopInstructions = PatchProcessor.GetOriginalInstructions(stopAudio).ToList();
int proxyStop = stopInstructions.FindIndex(code => code.operand is MethodInfo method && method.Name == "set_enabled");
int originalRestore = stopInstructions.FindIndex(code => code.operand is MethodInfo method && method.Name == "Restore"
    && method.DeclaringType.IsGenericType && method.DeclaringType.GetGenericTypeDefinition().Name == "VisibilityOverride`1"
    && method.DeclaringType.GetGenericArguments()[0].Name == "AudioListener");
Check(proxyStop >= 0 && originalRestore > proxyStop, "The proxy listener stops before the original listener is restored");
var audioTick = PatchProcessor.GetOriginalInstructions(AccessTools.Method(audioType, "Tick")).ToList();
int hideOriginal = audioTick.FindIndex(code => code.operand is MethodInfo method && method.Name == "Hide"
    && method.DeclaringType.IsGenericType && method.DeclaringType.GetGenericArguments()[0].Name == "AudioListener");
Check(hideOriginal >= 0 && audioTick.Skip(hideOriginal + 1).Any(code => code.operand is MethodInfo method && method.Name == "set_enabled"),
    "The original listener is disabled before the proxy is enabled");
Check(!AccessTools.GetDeclaredMethods(audioType).SelectMany(method => PatchProcessor.GetOriginalInstructions(method))
    .Any(code => code.opcode == OpCodes.Stfld && Equals(code.operand, velocityField)),
    "Audio adaptation never writes aircraft camera velocity");

// Validate the optional bridge against the actual installed third-party DLL,
// without instantiating it, changing its config or executing its flight law.
string wtPath = Path.Combine(gameDir, "BepInEx", "plugins", "NuclearOption-MouseAim", "NuclearOption-MouseAim.dll");
if (File.Exists(wtPath))
{
    var wt = Assembly.LoadFrom(wtPath);
    var wtBridge = pluginAssembly.GetType("ArcadeCam.Helpers.WtMouseAimCompatibility", true);
    var wtRewrite = AccessTools.Method(pluginAssembly.GetType("ArcadeCam.Patches.WtMouseAimPatches", true), "Transpiler");
    foreach (var method in new[] {
        AccessTools.Method(wt.GetType("NuclearOptionMouseAim.AimRig", true), "Update"),
        AccessTools.Method(wt.GetType("NuclearOptionMouseAim.WTMouseAimPlugin", true), "OnGUI") })
    {
        var before = PatchProcessor.GetOriginalInstructions(method).ToList();
        var after = ((IEnumerable<CodeInstruction>)wtRewrite.Invoke(null, new object[] { before, method })).ToList();
        bool gui = method.Name == "OnGUI";
        var modeWrapper = AccessTools.Method(wtBridge, "AimCameraMode");
        Check(after.Count == before.Count && after.Count(code => code.Calls(modeWrapper)) == (gui ? 1 : 2),
            "Installed WT 0.68 input/GUI mode checks adapt to ArcadeCam chase");
        if (gui)
        {
            Check(after.Count(code => code.Calls(AccessTools.Method(wtBridge, "ShowOverlay"))) == 1,
                "WT recording/toasts keep their path; only the flight overlay respects weapon HUD hiding");
            Check(after.Count(code => code.Calls(AccessTools.Method(wtBridge, "ShowGlocFade"))) == 1,
                "WT's extra G-LOC fade is adapted separately from its reticle");
        }
        for (int i = 0; i < before.Count; i++)
        {
            Check(before[i].labels.SequenceEqual(after[i].labels) && before[i].blocks.SequenceEqual(after[i].blocks),
                "WT compatibility preserves original branch/exception metadata");
            bool same = before[i].opcode == after[i].opcode && Equals(before[i].operand, after[i].operand);
            bool allowed = after[i].Calls(modeWrapper)
                || (gui && after[i].opcode == OpCodes.Nop)
                || (gui && after[i].Calls(AccessTools.Method(wtBridge, "ShowOverlay")))
                || (gui && after[i].Calls(AccessTools.Method(wtBridge, "ShowGlocFade")));
            Check(same || allowed, "WT compatibility changes only view/context/overlay guards");
        }
    }
    Check(AccessTools.PropertyGetter(wt.GetType("NuclearOptionMouseAim.AimRig", true), "AimForward")?.ReturnType == typeof(Vector3)
        && AccessTools.PropertyGetter(wt.GetType("NuclearOptionMouseAim.AimRig", true), "LookDelta")?.ReturnType == typeof(Vector2),
        "The installed WT aim and smoothed-look APIs match the adapter");
    Check(AccessTools.MethodDelegate<Func<Vector3>>(AccessTools.PropertyGetter(wt.GetType("NuclearOptionMouseAim.AimRig", true), "AimForward")) != null
        && AccessTools.MethodDelegate<Func<Vector2>>(AccessTools.PropertyGetter(wt.GetType("NuclearOptionMouseAim.AimRig", true), "LookDelta")) != null,
        "Delegates can bind the supported internal WT aim/look accessors");
    var wtCfg = wt.GetType("NuclearOptionMouseAim.Cfg", true);
    foreach (string setting in new[] { "Enabled", "WriteControl", "CameraFollow", "RightClickFreeze", "InvertPitch", "ShowOverlay", "GLocFadeEnabled" })
        Check(AccessTools.Field(wtCfg, setting)?.FieldType.GetGenericArguments().SingleOrDefault() == typeof(bool),
            "WT boolean compatibility setting has the supported API: " + setting);
    foreach (string setting in new[] { "MouseSensitivity", "OrbitAimSmoothing" })
        Check(AccessTools.Field(wtCfg, setting)?.FieldType.GetGenericArguments().SingleOrDefault() == typeof(float),
            "WT scalar compatibility setting has the supported API: " + setting);
}
else Console.WriteLine("SKIP: WT adapter IL validation (optional mod not installed).");
Check(!pluginAssembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "NuclearOption-MouseAim"),
    "ArcadeCam retains no hard assembly dependency on WT Mouse Aim");
var mouseViewType = pluginAssembly.GetType("ArcadeCam.Helpers.MouseAimView", true);
var mouseViewInstructions = PatchProcessor.GetOriginalInstructions(AccessTools.Method(mouseViewType, "Apply")).ToList();
var mouseFrameType = pluginAssembly.GetType("ArcadeCam.Helpers.MouseAimFrame", true);
Check(mouseViewInstructions.Any(code => code.Calls(AccessTools.Method(mouseFrameType, "Advance"))),
    "The production WT view uses the independently tested world-heading frame");
Check(!mouseViewInstructions.Any(code => code.Calls(AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.up)))),
    "WT view cannot inherit the airframe/base camera up vector");
Check(!mouseViewInstructions.Any(code => code.Calls(AccessTools.PropertyGetter(levelCameraType, "Active"))),
    "Choosing an aircraft-relative preset cannot switch WT aim back to a rolling reference");
var chaseUpdatePatch = pluginAssembly.GetType("ArcadeCam.Patches.CameraChaseStatePatches+UpdateState", true);
Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(chaseUpdatePatch, "Postfix"))
    .Any(code => code.Calls(AccessTools.PropertyGetter(mouseViewType, "ReferenceUp"))),
    "WT manual free look uses the stabilized aim frame");
Console.WriteLine($"PASS: {checks} native/third-party API, audio, camera integration and render-lifetime checks.");
