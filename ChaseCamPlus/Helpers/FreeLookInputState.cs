namespace ArcadeCam.Helpers;

/// <summary>Temporary UI input suppression must not erase a user's toggle choice.</summary>
internal sealed class FreeLookInputState
{
    private bool _toggleMode = true;
    private bool _toggled;
    private bool _ignoreToggleUntilRelease;
    private int _lastToggleFrame = -1;

    internal bool Read(bool held, bool pressed, bool toggleMode, bool allowed, int frame)
    {
        if (_toggleMode != toggleMode)
            Reset(frame, false, toggleMode);

        if (!allowed)
        {
            // Keep the chosen on/off state while map/menu input owns the controls.
            // A menu press held through closing must not flip it on return.
            _ignoreToggleUntilRelease = held;
            _lastToggleFrame = frame;
            return false;
        }

        if (!toggleMode)
            return held;

        if (_ignoreToggleUntilRelease)
        {
            _lastToggleFrame = frame;
            if (!held) _ignoreToggleUntilRelease = false;
            return _toggled;
        }

        // Camera and physics consumers must agree on the same Rewired press.
        if (pressed && _lastToggleFrame != frame)
        {
            _toggled = !_toggled;
            _lastToggleFrame = frame;
        }
        return _toggled;
    }

    internal void Reset(int frame, bool activeByDefault = false, bool toggleMode = true)
    {
        _toggleMode = toggleMode;
        _toggled = activeByDefault && toggleMode;
        _ignoreToggleUntilRelease = false;
        _lastToggleFrame = frame;
    }
}
