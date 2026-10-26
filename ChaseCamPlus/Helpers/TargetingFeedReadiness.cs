namespace ArcadeCam.Helpers;

// A created RenderTexture may still contain a previous camera's image. Wait for
// this sensor to finish rendering it. Do not assume it renders on every game frame.
internal sealed class TargetingFeedReadiness
{
    private int _camera, _texture, _started = -1, _rendered = -1;

    internal void Observe(int camera, int texture, int frame)
    {
        if (_started >= 0 && camera == _camera && texture == _texture) return;
        _camera = camera; _texture = texture; _started = frame; _rendered = -1;
    }

    internal void Rendered(int camera, int texture, int frame)
    {
        if (_started >= 0 && camera == _camera && texture == _texture && frame >= _started)
            _rendered = frame;
    }

    internal bool CanShow(int frame) => _started >= 0 && frame >= _started
        && _rendered >= _started && _rendered <= frame;

    internal void Reset() { _started = -1; _rendered = -1; }
}
