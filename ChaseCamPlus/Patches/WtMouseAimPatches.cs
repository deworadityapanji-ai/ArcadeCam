using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

// Installed dynamically only when the supported optional plugin is present.
// No flight-controller methods or native orbit/cockpit camera patches are removed.
internal static class WtMouseAimPatches
{
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var original = new List<CodeInstruction>(instructions);
        bool gui = __originalMethod.Name == "OnGUI";
        var mode = AccessTools.Field(typeof(CameraStateManager), nameof(CameraStateManager.cameraMode));
        int modes = original.FindAll(code => code.opcode == OpCodes.Ldsfld && Equals(code.operand, mode)).Count;
        if (modes != (gui ? 1 : 2)) return Unsupported(original);
        var rewritten = new List<CodeInstruction>();
        int overlays = 0, fades = 0;
        for (int i = 0; i < original.Count; i++)
        {
            CodeInstruction code = original[i];
            if (code.opcode == OpCodes.Ldsfld && Equals(code.operand, mode))
            {
                rewritten.Add(Call(code, nameof(WtMouseAimCompatibility.AimCameraMode)));
                continue;
            }
            if (gui && code.opcode == OpCodes.Ldsfld && code.operand is FieldInfo setting
                && setting.DeclaringType?.FullName == "NuclearOptionMouseAim.Cfg"
                && (setting.Name == "ShowOverlay" || setting.Name == "GLocFadeEnabled"))
            {
                if (i + 1 >= original.Count || original[i + 1].operand is not MethodInfo getter
                    || getter.Name != "get_Value" || original[i + 1].labels.Count != 0
                    || original[i + 1].blocks.Count != 0) return Unsupported(original);
                string name = setting.Name == "ShowOverlay" ? nameof(WtMouseAimCompatibility.ShowOverlay)
                    : nameof(WtMouseAimCompatibility.ShowGlocFade);
                rewritten.Add(Call(code, name));
                // The wrapped field/getter pair becomes one bool-returning call.
                // Retain a nop to preserve instruction positions and metadata.
                rewritten.Add(new CodeInstruction(OpCodes.Nop));
                if (setting.Name == "ShowOverlay") overlays++; else fades++;
                i++;
                continue;
            }
            rewritten.Add(code);
        }
        if (gui && (overlays != 1 || fades != 1)) return Unsupported(original);
        return rewritten;
    }

    private static CodeInstruction Call(CodeInstruction source, string method)
    {
        var code = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WtMouseAimCompatibility), method));
        code.labels.AddRange(source.labels); code.blocks.AddRange(source.blocks);
        return code;
    }

    private static IEnumerable<CodeInstruction> Unsupported(List<CodeInstruction> original)
    {
        WtMouseAimCompatibility.Disable("WT Mouse Aim bridge: input/GUI layout changed; compatibility disabled and that method left unchanged.");
        return original;
    }
}
