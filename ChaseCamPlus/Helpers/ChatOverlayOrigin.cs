using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Helpers;

// The chat/feed shares screen UI with other gameplay widgets. Stabilize its
// overlay canvas, leaving messages, chat focus, visibility and world-space UI alone.
internal sealed class ChatOverlayOrigin
{
    private static readonly FieldInfo MessageText = AccessTools.Field(typeof(MessageUI), "messageText");
    private static readonly FieldInfo KillFeedText = AccessTools.Field(typeof(MessageUI), "killFeedText");
    private static readonly FieldInfo Background = AccessTools.Field(typeof(MessageUI), "messageBackground");
    private static int _weaponReports, _chaseReports;
    private static int _reportedRoot, _rootReports;
    private readonly Vector3 _origin = Datum.originPosition;
    private readonly OriginShiftSnapshot<Canvas> _snapshot = new(
        IsScreenOnlyRoot,
        canvas => canvas.transform.position,
        (canvas, position) => canvas.transform.position = position);

    private static bool IsScreenOnlyRoot(Canvas canvas) =>
        canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay
        // A mixed scene root must still move its world/camera/physics children.
        // Protect a UI-only subtree, never an ancestor of the actual world rig.
        && canvas.GetComponentInChildren<Camera>(true) == null
        && canvas.GetComponentInChildren<Rigidbody>(true) == null
        && (Datum.origin == null || !Datum.origin.IsChildOf(canvas.transform));

    internal void Capture()
    {
        GameplayUI gameplay = SceneSingleton<GameplayUI>.i;
        Add(gameplay == null ? null : gameplay.gameplayCanvas);
        MessageUI messages = SceneSingleton<MessageUI>.i;
        if (messages == null) return;
        AddParent(messages);
        AddParent(MessageText?.GetValue(messages) as Component);
        AddParent(KillFeedText?.GetValue(messages) as Component);
        AddParent((Background?.GetValue(messages) as GameObject)?.transform);
    }

    private void AddParent(Component component)
    {
        if (component != null) Add(component.GetComponentInParent<Canvas>(true));
    }

    private void Add(Canvas canvas)
    {
        if (canvas == null) return;
        Canvas root = canvas.rootCanvas;
        if (root == null) return;
        if (root.GetInstanceID() != _reportedRoot && _rootReports < 3)
        {
            _reportedRoot = root.GetInstanceID(); _rootReports++;
            Plugin.Logger.LogInfo($"Chat overlay canvas: {root.name}, mode={root.renderMode}.");
        }
        _snapshot.Capture(root);
    }

    internal void Restore()
    {
        // Datum records its translated world position after the scene roots
        // move, so this delta has the same sign as the canvas-root movement.
        Vector3 delta = Datum.originPosition - _origin;
        int count = _snapshot.Restore(delta);
        if (count == 0) return;
        bool weapon = WeaponCamera.IsDisplaying;
        if (weapon ? _weaponReports++ < 3 : _chaseReports++ < 1)
            Plugin.Logger.LogInfo($"Chat overlay: protected {count} screen-space canvas(es) from origin shift {delta}; weapon view={weapon}.");
    }
}
