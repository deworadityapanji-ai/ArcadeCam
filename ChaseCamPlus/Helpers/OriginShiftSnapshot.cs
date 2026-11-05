using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Short-lived snapshots bracket a world-origin shift, not an entire camera
// session. Re-anchor only a screen root that moved by that exact world shift.
internal sealed class OriginShiftSnapshot<T> where T : class
{
    private readonly Dictionary<T, Vector3> _positions = new();
    private readonly Func<T, bool> _eligible;
    private readonly Func<T, Vector3> _read;
    private readonly Action<T, Vector3> _write;

    internal OriginShiftSnapshot(Func<T, bool> eligible, Func<T, Vector3> read, Action<T, Vector3> write)
    { _eligible = eligible; _read = read; _write = write; }

    internal void Capture(T node)
    {
        if (node != null && _eligible(node) && !_positions.ContainsKey(node))
            _positions.Add(node, _read(node));
    }

    internal int Restore(Vector3 originDelta)
    {
        int corrected = 0;
        try
        {
            if (!Finite(originDelta) || originDelta.sqrMagnitude < 0.0001f) return 0;
            foreach (var entry in _positions)
            {
                if (!_eligible(entry.Key)) continue;
                Vector3 current = _read(entry.Key);
                // A canvas Unity already re-anchored, or a legitimate layout
                // change, must not receive a second compensation.
                if (!Finite(entry.Value) || !Finite(current)
                    || (current - (entry.Value + originDelta)).sqrMagnitude > 0.0001f) continue;
                _write(entry.Key, entry.Value);
                corrected++;
            }
            return corrected;
        }
        finally { _positions.Clear(); }
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
        && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
