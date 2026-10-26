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
Console.WriteLine($"PASS: {checks} native input/impact API, render-lifetime and shortcut-priority checks.");
