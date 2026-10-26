using System.Collections.Generic;
using System.Reflection.Emit;
using ArcadeCam.Helpers;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Patches;

[HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.CheckInput))]
internal static class ChasePresetInputPatches
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = new List<CodeInstruction>(instructions);
        var codes = new List<CodeInstruction>();
        var getKeyDown = AccessTools.Method(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) });
        var replacement = AccessTools.Method(typeof(ChasePresetInputPatches), nameof(GetKeyDown));
        int count = 0;
        foreach (var code in original)
        {
            if (!code.Calls(getKeyDown)) { codes.Add(code); continue; }
            var call = new CodeInstruction(OpCodes.Call, replacement);
            call.labels.AddRange(code.labels);
            call.blocks.AddRange(code.blocks);
            codes.Add(call);
            count++;
        }
        // Ten numbered views and the editor's H shortcut. The wrapper leaves H
        // untouched. Validate the native method rather than silently missing it.
        if (count == 11) return codes;
        Plugin.Logger.LogWarning($"Chase preset input: expected 11 key reads, found {count}; leaving native shortcuts unchanged.");
        return original;
    }

    internal static bool GetKeyDown(KeyCode key)
    {
        bool keyDown = Input.GetKeyDown(key);
        if (!keyDown) return false;
        var player = GameManager.playerInput;
        bool weaponRequested = Plugin.IsActive && Plugin.WeaponCameraEnabled.Value
            && Plugin.WeaponCameraActionId >= 0 && player != null
            && ChaseInput.IsAllowed(SceneSingleton<CameraStateManager>.i)
            && player.GetButtonDown(Plugin.WeaponCameraActionId);
        return ChasePresetInput.Read(key, keyDown, weaponRequested);
    }
}
