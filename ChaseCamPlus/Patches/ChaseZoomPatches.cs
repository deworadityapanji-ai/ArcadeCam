using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.UpdateState))]
internal static class ChaseZoomPatches
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = new List<CodeInstruction>(instructions);
        var codes = new List<CodeInstruction>(original);
        var clamp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp),
            new[] { typeof(float), typeof(float), typeof(float) });
        var minimum = AccessTools.Method(typeof(ChaseCameraGeometry),
            nameof(ChaseCameraGeometry.MinimumZoomAdjustment));
        int count = 0;
        for (int i = 2; i + 1 < codes.Count; i++)
        {
            if (!codes[i].Calls(clamp)
                || codes[i - 2].opcode != OpCodes.Ldc_R4 || !Equals(codes[i - 2].operand, 0f)
                || codes[i - 1].opcode != OpCodes.Ldc_R4 || !Equals(codes[i - 1].operand, 10f)
                || codes[i + 1].opcode != OpCodes.Stfld
                || !(codes[i + 1].operand is FieldInfo field) || field.Name != "viewDistAdjust")
                continue;

            var call = new CodeInstruction(OpCodes.Call, minimum);
            call.labels.AddRange(codes[i - 2].labels);
            call.blocks.AddRange(codes[i - 2].blocks);
            codes[i - 2] = call;
            count++;
        }
        if (count == 1) return codes;
        Plugin.Logger.LogWarning(
            $"Closer chase zoom: expected one distance clamp, found {count}; leaving zoom limits unchanged.");
        return original;
    }
}