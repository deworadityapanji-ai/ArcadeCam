using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcadeCam.Helpers;
using HarmonyLib;

namespace ArcadeCam.Patches;

// The kill feed message (killer, victim, victim type) drives the kill cam and the DESTROYED callout (sent to UI Plus).
// Observed only; the native kill feed is unchanged.
[HarmonyPatch]
internal static class KillMessagePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo m in AccessTools.GetDeclaredMethods(typeof(MessageManager)))
        {
            if (!m.Name.StartsWith("UserCode_RpcKillMessage")) continue;
            var p = m.GetParameters();
            if (p.Length == 3 && p[0].ParameterType == typeof(PersistentID) && p[1].ParameterType == typeof(PersistentID))
                yield return m;
        }
    }

    private static bool Prepare() => TargetMethods().Any();

    private static void Prefix(object[] __args)
    {
        KillCam.OnKill((PersistentID)__args[0], (PersistentID)__args[1], Convert.ToInt32(__args[2]));
    }
}
