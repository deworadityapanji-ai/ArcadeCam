using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ChaseCamPlus.Helpers;
using HarmonyLib;

namespace ChaseCamPlus.Patches;

public class TurretPatches
{
    /// <summary>
    /// Manual turret aiming reads the active camera, but only while the cockpit camera is the one
    /// running:
    ///
    /// <code>
    /// if (aircraft.LocalSim &amp;&amp; i.currentState == i.cockpitState)
    /// {
    ///     SetVector(i.transform.forward);
    ///     ...
    /// }
    /// AimTurret(manualVector);
    /// </code>
    ///
    /// In chase nothing writes <c>manualVector</c>, so the turret holds its last commanded heading —
    /// aim left in the cockpit, switch to chase, and the turret stays left for the rest of the
    /// flight. The crosshair is not stale in that case; the weapon really is stuck.
    ///
    /// The method reads <c>cockpitState</c> exactly once, so swapping that single field load for
    /// <see cref="PatchHelper.AimAllowedState"/> is enough. It consumes the same
    /// <c>CameraStateManager</c> already on the stack and returns a state, leaving the comparison
    /// and the branch untouched.
    /// </summary>
    [HarmonyPatch(typeof(Turret), "FixedUpdate")]
    static class FixedUpdate
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var replacement = AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.AimAllowedState));

            int patched = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldfld)
                    continue;

                if (codes[i].operand is not FieldInfo field)
                    continue;

                if (field.Name != nameof(CameraStateManager.cockpitState) ||
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
                    $"Turret.FixedUpdate: expected exactly 1 load of CameraStateManager.cockpitState, "
                    + $"found {patched}. The game has probably been updated — leaving this method "
                    + "unpatched, turret aiming stays cockpit-only.");
                return instructions;
            }

            return SwapAimDirection(codes);
        }

        /// <summary>
        /// Retargets the camera's <c>forward</c> read that feeds <c>SetVector</c>, so the direction
        /// handed to the turret can be chosen per camera.
        ///
        /// The method reads <c>forward</c> twice — the other one is the turret's own transform, used
        /// for the line-of-fire check — so matching the property alone would hit the wrong site.
        /// The two are told apart by what the transform was read from: the turret's own comes off
        /// <c>this</c>, so anything that does not start at argument zero is the camera's.
        ///
        /// Identifying it the other way round, by requiring the singleton access that precedes it,
        /// is what a first attempt did — and it matched nothing, because <c>SceneSingleton&lt;T&gt;.i</c>
        /// is a field rather than a property and so compiles to a load, not a call. Excluding the
        /// one site we can name is not sensitive to how the other one is reached.
        /// </summary>
        private static IEnumerable<CodeInstruction> SwapAimDirection(List<CodeInstruction> codes)
        {
            var replacement = AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.AimDirection));

            int patched = 0;
            for (int i = 2; i < codes.Count; i++)
            {
                if (!IsCallTo(codes[i], "get_forward", typeof(UnityEngine.Transform)))
                    continue;

                if (!IsCallTo(codes[i - 1], "get_transform", null))
                    continue;

                // The turret's own transform.forward, used for the line-of-fire check: skip it.
                if (codes[i - 2].IsLdarg(0))
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
                    "Turret.FixedUpdate: expected exactly 1 camera forward read feeding the turret, "
                    + $"found {patched}. Turret aiming in chase will follow the camera, but "
                    + "TurretAimInChase=false will not recentre it.");

                // Named so a future mismatch can be diagnosed from the log without a debugger.
                foreach (CodeInstruction code in codes)
                {
                    if (IsCallTo(code, "get_forward", typeof(UnityEngine.Transform)))
                        Plugin.Logger.LogInfo($"  forward read seen: {code}");
                }
            }

            return codes;
        }

        private static bool IsCallTo(CodeInstruction code, string name, System.Type declaringType)
        {
            if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt)
                return false;

            if (code.operand is not MethodInfo method || method.Name != name)
                return false;

            return declaringType == null || method.DeclaringType == declaringType;
        }
    }
}
