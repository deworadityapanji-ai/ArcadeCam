using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ChaseCamPlus.Helpers;

/// <summary>
/// Debug aid, off by default. Writes the UI hierarchy around the flight HUD to the BepInEx log.
///
/// Some of what the HUD draws cannot be identified from the decompiled game code, because which
/// object owns a given widget is scene data rather than C#. This prints the tree so those objects
/// can be named instead of guessed at.
/// </summary>
public static class HierarchyDump
{
    private const int MaxLines = 1200;

    public static void Poll()
    {
        if (!Plugin.DebugDumpHudHierarchy.Value)
            return;

        if (!Input.GetKeyDown(Plugin.DebugDumpKey.Value))
            return;

        Dump();
    }

    private static void Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== ChaseCamPlus HUD hierarchy dump =====");
        sb.AppendLine($"camera mode: {CameraStateManager.cameraMode}, mapMaximized: {DynamicMap.mapMaximized}");

        FlightHud hud = SceneSingleton<FlightHud>.i;
        GameObject hudCanvas = hud != null ? CanvasObject(hud) : null;

        if (hudCanvas != null)
        {
            // From the root, so siblings of the canvas show up too — the object we are looking for
            // may well not be underneath it.
            sb.AppendLine($"--- root of FlightHud canvas ({hudCanvas.name}) ---");
            Walk(sb, hudCanvas.transform.root, 0, hudCanvas.transform);
        }
        else
        {
            sb.AppendLine("--- FlightHud canvas not found ---");
        }

        DynamicMap map = SceneSingleton<DynamicMap>.i;
        if (map != null)
        {
            sb.AppendLine($"--- DynamicMap root ({map.gameObject.name}, active: {map.gameObject.activeSelf}) ---");
            if (map.transform.root != hudCanvas?.transform.root)
                Walk(sb, map.transform.root, 0, map.transform);
            else
                sb.AppendLine("(same tree as above, marked <== DYNAMICMAP)");
        }

        AppendDesignatorState(sb);

        sb.AppendLine("===== end of dump =====");
        Plugin.Logger.LogInfo(sb.ToString());
    }

    /// <summary>
    /// The target designator is hidden through its component rather than its GameObject — some HUD
    /// weapon states write <c>enabled</c>, others only fade the colour's alpha — so neither shows up
    /// in a tree of active objects. Report both, plus the gear state the writers key off.
    /// </summary>
    private static void AppendDesignatorState(StringBuilder sb)
    {
        CombatHUD combat = SceneSingleton<CombatHUD>.i;
        if (combat == null)
        {
            sb.AppendLine("--- targetDesignator: no CombatHUD ---");
            return;
        }

        object designator = AccessTools.Field(typeof(CombatHUD), "targetDesignator")?.GetValue(combat);
        if (designator == null)
        {
            sb.AppendLine("--- targetDesignator: field not found ---");
            return;
        }

        string enabled = designator is Behaviour behaviour ? behaviour.enabled.ToString() : "?";
        object colour = AccessTools.Property(designator.GetType(), "color")?.GetValue(designator);
        string alpha = colour is Color c ? c.a.ToString("F2") : "?";

        Aircraft aircraft = combat.aircraft;
        string gear = aircraft != null ? aircraft.gearDeployed.ToString() : "no aircraft";

        sb.AppendLine($"--- targetDesignator: enabled={enabled}, alpha={alpha}, gearDeployed={gear} ---");
        sb.AppendLine("(hidden by enabled=False, or by alpha at or near 0)");
    }

    private static int _lines;

    private static void Walk(StringBuilder sb, Transform node, int depth, Transform marked)
    {
        if (depth == 0)
            _lines = 0;

        if (_lines++ > MaxLines)
        {
            if (_lines == MaxLines + 2)
                sb.AppendLine("... truncated ...");
            return;
        }

        var line = new StringBuilder();
        line.Append(' ', depth * 2);
        line.Append(node.name);
        line.Append(node.gameObject.activeSelf ? "" : "  [INACTIVE]");

        if (node == marked)
            line.Append("  <== ");

        // Component type names are enough to recognise what a node is without referencing the UI
        // assemblies for their concrete types.
        Component[] components = node.GetComponents<Component>();
        if (components.Length > 0)
        {
            line.Append("  {");
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0) line.Append(", ");

                if (components[i] == null)
                {
                    line.Append("null");
                    continue;
                }

                line.Append(components[i].GetType().Name);

                // A disabled component is invisible in a tree of active GameObjects, and that is how
                // several HUD widgets are actually hidden.
                if (components[i] is Behaviour b && !b.enabled)
                    line.Append(":off");
            }
            line.Append('}');
        }

        // Screen position helps pick the bottom-left widget out of a long list.
        if (node is RectTransform rect)
            line.Append($"  pos={rect.anchoredPosition}");

        sb.AppendLine(line.ToString());

        for (int i = 0; i < node.childCount; i++)
            Walk(sb, node.GetChild(i), depth + 1, marked);
    }

    private static FieldInfo _canvasField;

    private static GameObject CanvasObject(FlightHud hud)
    {
        _canvasField ??= AccessTools.Field(typeof(FlightHud), "canvas");
        return _canvasField?.GetValue(hud) is Component canvas ? canvas.gameObject : hud.gameObject;
    }
}
