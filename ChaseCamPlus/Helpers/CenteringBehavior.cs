namespace ArcadeCam.Helpers;

public enum CenteringBehavior { OnInputRelease, ConstantPull }
internal enum CenteringAction { None, Pull, Snap }

internal sealed class CenteringInputState
{
    private float _idleTime;
    private bool _snapped;

    internal CenteringAction Read(CenteringBehavior mode, bool active, bool trackingTarget,
        bool moving, float force, float delay, float deltaTime)
    {
        if (!active || trackingTarget || force <= 0f)
        {
            Reset();
            return CenteringAction.None;
        }
        if (moving)
        {
            _idleTime = 0f;
            _snapped = false;
        }
        else
        {
            _idleTime += System.Math.Max(deltaTime, 0f);
        }

        if (mode == CenteringBehavior.ConstantPull)
            return CenteringAction.Pull;
        if (moving || _idleTime < System.Math.Max(delay, 0f))
            return CenteringAction.None;
        if (force >= 1f)
        {
            if (_snapped) return CenteringAction.None;
            _snapped = true;
            return CenteringAction.Snap;
        }
        _snapped = false;
        return CenteringAction.Pull;
    }

    internal void Reset()
    {
        _idleTime = 0f;
        _snapped = false;
    }
}