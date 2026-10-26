using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Helpers;

// ScreenSpaceOverlay UI can draw after SRP endFrameRendering. Keep only the
// native flight/combat/helmet/map canvases hidden for the whole weapon session.
// Canvas.enabled changes drawing, never GameObject activation or flight input.
internal static class WeaponCameraHud
{
    private static readonly FieldInfo HudCanvas = AccessTools.Field(typeof(FlightHud), "canvas");
    private static readonly VisibilityOverride<Canvas> Visibility = new(
        canvas => canvas != null, canvas => canvas.enabled, (canvas, enabled) => canvas.enabled = enabled);
    private static readonly HashSet<Canvas> Roots = new();
    private static readonly List<Canvas> Children = new();

    internal static void Hide()
    {
        Roots.Clear();
        FlightHud hud = SceneSingleton<FlightHud>.i;
        HideCanvas(hud == null ? null : HudCanvas?.GetValue(hud) as Canvas);
        HideParentCanvas(SceneSingleton<CombatHUD>.i);
        HideParentCanvas(SceneSingleton<HeadMountedDisplay>.i);
        HideParentCanvas(SceneSingleton<DynamicMap>.i);
        Visibility.Reapply();
    }

    private static void HideParentCanvas(Component component)
    {
        if (component == null) return;
        for (Transform node = component.transform; node != null; node = node.parent)
        {
            Canvas canvas = node.GetComponent<Canvas>();
            if (canvas != null) { HideCanvas(canvas); return; }
        }
    }

    private static void HideCanvas(Canvas canvas)
    {
        if (canvas == null || !Roots.Add(canvas)) return;
        canvas.GetComponentsInChildren(true, Children);
        foreach (Canvas child in Children) Visibility.Hide(child);
    }

    internal static void Reapply() => Visibility.Reapply();
    internal static void Restore()
    {
        Visibility.Restore(); Roots.Clear(); Children.Clear();
    }
}
