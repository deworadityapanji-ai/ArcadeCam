using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ChaseCamPlus.Helpers;
using HarmonyLib;

namespace ChaseCamPlus.Patches;

/// <summary>
/// Workaround for a vanilla bug, gated behind the <c>FixWeaponFoV</c> config flag (off by default).
///
/// Selecting an unguided bomb runs <c>HUDBombingState.SetHUDWeaponState</c>, which calls
/// <c>SetDesiredFoV(PlayerSettings.defaultFoV, 0f)</c> without looking at the active camera, so an
/// external view gets yanked to the cockpit FoV. Nothing restores it: of the nine HUD weapon states
/// only this one and <c>HUDBoresightState</c> touch the FoV at all, so switching to any other weapon
/// leaves the wrong value in place until a camera state change happens to reset it.
///
/// Both sites are corrected the same way, by swapping the load of <c>PlayerSettings.defaultFoV</c>
/// for <see cref="PatchHelper.CorrectDefaultFoV"/>. Replacing the field load rather than rewriting
/// the surrounding call keeps the boresight's <c>* 0.7f</c> zoom and its <c>zoomOnBoresight</c> gate
/// working exactly as designed — only the base value changes.
/// </summary>
public class WeaponFoVPatches
{
    [HarmonyPatch(typeof(HUDBombingState), "SetHUDWeaponState")]
    static class BombingSetHUDWeaponState
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SwapDefaultFoV(instructions, "HUDBombingState.SetHUDWeaponState", expected: 1);
    }

    [HarmonyPatch(typeof(HUDBoresightState), "UpdateWeaponDisplay")]
    static class BoresightUpdateWeaponDisplay
    {
        // Two loads here: the ternary reads defaultFoV in both branches.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            SwapDefaultFoV(instructions, "HUDBoresightState.UpdateWeaponDisplay", expected: 2);
    }

    private static IEnumerable<CodeInstruction> SwapDefaultFoV(
        IEnumerable<CodeInstruction> instructions, string where, int expected)
    {
        var codes = new List<CodeInstruction>(instructions);
        var replacement = AccessTools.Method(typeof(PatchHelper), nameof(PatchHelper.CorrectDefaultFoV));

        int patched = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode != OpCodes.Ldsfld)
                continue;

            if (codes[i].operand is not FieldInfo field)
                continue;

            if (field.Name != nameof(PlayerSettings.defaultFoV) ||
                field.DeclaringType != typeof(PlayerSettings))
                continue;

            var call = new CodeInstruction(OpCodes.Call, replacement);
            call.labels.AddRange(codes[i].labels);
            call.blocks.AddRange(codes[i].blocks);
            codes[i] = call;
            patched++;
        }

        if (patched != expected)
        {
            Plugin.Logger.LogError(
                $"{where}: expected {expected} load(s) of PlayerSettings.defaultFoV, found {patched}. "
                + "The game has probably been updated (possibly with a real fix for this) — "
                + "leaving this method unpatched.");
            return instructions;
        }

        return codes;
    }
}
