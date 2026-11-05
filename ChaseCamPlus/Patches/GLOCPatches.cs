using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

[HarmonyPatch]
internal static class GLOCPatches
{
    [HarmonyTargetMethods]
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GLOC), nameof(GLOC.SimulateGLOC));
        yield return AccessTools.Method(typeof(GLOC), "GLOC_OnSwitchCamera");
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var original = new List<CodeInstruction>(instructions);
        var mode = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraMode));
        int reads = original.FindAll(code => code.opcode == OpCodes.Ldsfld && Equals(code.operand, mode)).Count;
        if (reads != 1)
        {
            Plugin.Logger.LogWarning($"G-LOC {__originalMethod.Name}: expected 1 camera-mode read, found {reads}; leaving this method unchanged.");
            return original;
        }

        var replacement = AccessTools.Method(typeof(GlocView), nameof(GlocView.EffectiveCameraMode));
        var codes = new List<CodeInstruction>(original.Count + 1);
        foreach (var code in original)
        {
            if (code.opcode != OpCodes.Ldsfld || !Equals(code.operand, mode))
            {
                codes.Add(code);
                continue;
            }

            // Branches must land on the instance load, so every route supplies
            // the GLOC owner to the replacement camera-mode check.
            var instance = new CodeInstruction(OpCodes.Ldarg_0);
            instance.labels.AddRange(code.labels);
            instance.blocks.AddRange(code.blocks);
            codes.Add(instance);
            codes.Add(new CodeInstruction(OpCodes.Call, replacement));
        }
        return codes;
    }
}
