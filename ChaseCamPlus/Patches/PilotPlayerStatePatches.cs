using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ChaseCamPlus.Helpers;
using HarmonyLib;

namespace ChaseCamPlus.Patches;

public class PilotPlayerStatePatches
{
    /// <summary>
    /// Vanilla <c>PlayerAxisControls</c> only feeds mouse movement into the virtual joystick while
    /// <c>CameraStateManager.cameraMode == CameraMode.cockpit</c>. The rest of the method — reading
    /// the stick position back out into pitch/roll/yaw — already runs in every camera mode, which is
    /// why the stick stays frozen but still commanding once you switch to chase.
    ///
    /// The method reads that static field exactly once, so swapping that single load for a call to
    /// <see cref="PatchHelper.EffectiveCameraMode"/> is enough. Replacing the load rather than the
    /// comparison keeps the patch independent of how the compiler emitted the branch, and leaves the
    /// evaluation stack untouched.
    /// </summary>
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    static class PlayerAxisControls
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var replacement = AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.EffectiveCameraMode));

            int patched = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldsfld)
                    continue;

                if (codes[i].operand is not FieldInfo field)
                    continue;

                if (field.Name != nameof(CameraStateManager.cameraMode) ||
                    field.DeclaringType != typeof(CameraStateManager))
                    continue;

                var call = new CodeInstruction(OpCodes.Call, replacement);
                call.labels.AddRange(codes[i].labels);
                call.blocks.AddRange(codes[i].blocks);
                codes[i] = call;
                patched++;
            }

            if (patched != 1)
            {
                Plugin.Logger.LogError(
                    $"PlayerAxisControls: expected exactly 1 load of CameraStateManager.cameraMode, found {patched}. "
                    + "The game has probably been updated — leaving this method unpatched, "
                    + "the virtual joystick will behave like vanilla.");
                return instructions;
            }

            return codes;
        }
    }
}
