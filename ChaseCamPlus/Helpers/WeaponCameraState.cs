using System;

namespace ArcadeCam.Helpers;

internal enum WeaponViewPhase { Off, Following, Impact, Returning }

// Pure lifecycle/input state. The rendered view never owns the aircraft or input.
internal sealed class WeaponCameraState
{
    internal WeaponViewPhase Phase { get; private set; }
    internal int? TrackedToken { get; private set; }
    internal float PhaseTime { get; private set; }
    private bool _held, _blocked, _toggleMode, _modeKnown;
    private float _lossTime;
    internal bool Displaying => Phase != WeaponViewPhase.Off;

    internal bool Input(bool held, bool down, bool toggle, bool allowed, int? latestToken)
    {
        _held = held;
        if (!held) _blocked = false;
        if (_modeKnown && _toggleMode != toggle)
        {
            Cancel(held); _toggleMode = toggle;
            return false;
        }
        _modeKnown = true; _toggleMode = toggle;
        if (!allowed) { Cancel(held); return false; }
        if (_blocked) return false;
        if (Phase == WeaponViewPhase.Following || Phase == WeaponViewPhase.Impact)
        {
            if (toggle ? down : !held) Return();
            return false;
        }
        if ((toggle ? down : held) && latestToken.HasValue)
        {
            Phase = WeaponViewPhase.Following; PhaseTime = 0f; _lossTime = 0f;
            TrackedToken = latestToken; return true;
        }
        return false;
    }

    internal void Detonated(float impactSeconds)
    {
        if (Phase != WeaponViewPhase.Following) return;
        if (impactSeconds <= 0f) { Return(); return; }
        Phase = WeaponViewPhase.Impact; PhaseTime = 0f;
    }

    internal void Lost(float deltaTime = 0f, float detonationGrace = 0f)
    {
        if (Phase != WeaponViewPhase.Following) return;
        _lossTime += Math.Max(0f, deltaTime);
        if (_lossTime >= Math.Max(0f, detonationGrace)) Return();
    }

    internal void Return()
    {
        if (!Displaying || Phase == WeaponViewPhase.Returning) return;
        Phase = WeaponViewPhase.Returning; PhaseTime = 0f;
        _blocked = _held;
    }

    internal void Advance(float deltaTime, float impactSeconds, float returnSeconds)
    {
        float dt = Math.Max(0f, deltaTime);
        if (Phase == WeaponViewPhase.Impact)
        {
            PhaseTime += dt;
            if (PhaseTime < Math.Max(0f, impactSeconds)) return;
            dt = Math.Max(0f, PhaseTime - Math.Max(0f, impactSeconds));
            Return();
        }
        if (Phase != WeaponViewPhase.Returning) return;
        PhaseTime += dt;
        if (PhaseTime >= Math.Max(0f, returnSeconds)) Cancel(_held);
    }

    internal float ReturnBlend(float seconds)
    {
        float t = seconds <= 0f ? 1f : Math.Max(0f, Math.Min(1f, PhaseTime / seconds));
        return t * t * (3f - 2f * t);
    }

    internal void Cancel(bool requireRelease = false)
    {
        Phase = WeaponViewPhase.Off; PhaseTime = 0f; TrackedToken = null;
        _blocked = requireRelease;
    }
}
