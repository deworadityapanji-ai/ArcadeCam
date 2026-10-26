using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Smooth the boom, never the weapon's translation. An absolute-position lerp
// trails a fast weapon by speed * smoothing time even after acquisition.
internal sealed class WeaponCameraFollow
{
    internal bool Initialized { get; private set; }
    internal Vector3 Offset { get; private set; }

    internal void Reset() { Initialized = false; Offset = Vector3.zero; }

    internal Vector3 Advance(Vector3 wantedOffset, float dt)
    {
        if (!Initialized)
        {
            // Every activation, including re-entry during return, acquires the
            // live weapon immediately instead of flying out from the aircraft.
            Offset = wantedOffset;
            Initialized = true;
        }
        else
        {
            float blend = dt <= 0f ? 0f : 1f - (float)Math.Exp(-dt / 0.18f);
            Offset = Vector3.Lerp(Offset, wantedOffset, blend);
        }
        return Offset;
    }
}
