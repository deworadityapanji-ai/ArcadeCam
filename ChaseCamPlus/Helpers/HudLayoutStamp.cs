using UnityEngine;

namespace ArcadeCam.Helpers;

// Presentation layout is stable across camera movement. Only a real canvas,
// resolution/scale or attachment-mode change requires recapturing native layout.
internal readonly struct HudLayoutStamp
{
    private readonly int _canvas;
    private readonly Vector2 _size;
    private readonly Vector3 _scale;
    private readonly HudAttachment _left, _right, _compass, _waterline;

    internal HudLayoutStamp(int canvas, Vector2 size, Vector3 scale,
        HudAttachment left, HudAttachment right, HudAttachment compass, HudAttachment waterline)
    {
        _canvas = canvas; _size = size; _scale = scale;
        _left = left; _right = right; _compass = compass; _waterline = waterline;
    }

    internal bool Matches(HudLayoutStamp other) => _canvas == other._canvas
        && (_size - other._size).sqrMagnitude < 0.0001f
        && (_scale - other._scale).sqrMagnitude < 0.00000001f
        && _left == other._left && _right == other._right
        && _compass == other._compass && _waterline == other._waterline;
}
