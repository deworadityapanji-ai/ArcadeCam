using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Patches;

[HarmonyPatch]
internal static class WeaponExplosionAudioPosition
{
    [HarmonyTargetMethods]
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ExplosionAudio), "Start");
        yield return AccessTools.Method(typeof(ExplosionAudioManager), "Update");
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var original = new List<CodeInstruction>(instructions);
        var position = AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));
        int expected = __originalMethod.DeclaringType == typeof(ExplosionAudio) ? 2 : 1;
        if (original.FindAll(code => code.Calls(position)).Count != expected)
        {
            Plugin.Logger.LogWarning($"Weapon audio: unexpected position reads in {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}; leaving native audio unchanged.");
            return original;
        }
        var replacement = AccessTools.Method(typeof(WeaponCameraAudio), nameof(WeaponCameraAudio.ListenerPosition));
        var rewritten = new List<CodeInstruction>();
        foreach (var code in original)
        {
            if (!code.Calls(position)) { rewritten.Add(code); continue; }
            var call = new CodeInstruction(OpCodes.Call, replacement);
            call.labels.AddRange(code.labels); call.blocks.AddRange(code.blocks);
            rewritten.Add(call);
        }
        return rewritten;
    }
}

[HarmonyPatch(typeof(SonicBoomManager), nameof(SonicBoomManager.ManageSonicBooms))]
internal static class WeaponSonicAudioPosition
{
    [HarmonyPrefix]
    static void Prefix(ref GlobalPosition cameraPos) => WeaponCameraAudio.UseListenerPosition(ref cameraPos);
}

[HarmonyPatch]
internal static class WeaponSonicAudioVelocity
{
    [HarmonyPrepare]
    static bool Prepare() => TargetMethod() != null;

    [HarmonyTargetMethod]
    internal static MethodBase TargetMethod() => AccessTools.Method(
        AccessTools.Inner(typeof(SonicBoomManager), "ManagedSonicBoom"), "Manage");

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = new List<CodeInstruction>(instructions);
        var field = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraVelocity));
        if (original.FindAll(code => code.opcode == OpCodes.Ldfld && Equals(code.operand, field)).Count != 1)
        {
            Plugin.Logger.LogWarning("Weapon audio: unexpected sonic-boom velocity reads; leaving native relative velocity unchanged.");
            return original;
        }
        var replacement = AccessTools.Method(typeof(WeaponCameraAudio), nameof(WeaponCameraAudio.ListenerVelocity));
        var rewritten = new List<CodeInstruction>();
        foreach (var code in original)
        {
            if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, field)) { rewritten.Add(code); continue; }
            var call = new CodeInstruction(OpCodes.Call, replacement);
            call.labels.AddRange(code.labels); call.blocks.AddRange(code.blocks);
            rewritten.Add(call);
        }
        return rewritten;
    }
}
