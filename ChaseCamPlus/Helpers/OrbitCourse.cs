using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Course and offset lengths are independent of roll, vertical velocity, origin
// position and camera FoV. There is no inertial boom, stretch or pitch-follow.
internal sealed class OrbitCourse
{
    private Vector3 _course;
    private bool _ready;

    internal void Reset() { _ready = false; _course = Vector3.forward; }

    internal Vector3 Advance(Vector3 velocity, Vector3 forward, float dt)
    {
        Vector3 wanted = Horizontal(velocity + forward * 10f);
        if (!_ready)
        {
            _course = Horizontal(velocity * 10f + forward);
            if (_course.sqrMagnitude < 0.001f) _course = Vector3.forward;
            _ready = true;
        }
        if (wanted.sqrMagnitude >= 0.001f)
        {
            Vector3 next = Vector3.Lerp(_course, wanted, Math.Max(0f, Math.Min(1f, dt * 10f)));
            // An exact reversal can cancel the vector. Take the new course in
            // that case; the rig's rotation smoothing handles the hand-over.
            _course = next.sqrMagnitude >= 0.001f ? next : wanted;
        }
        return _course.normalized;
    }

    internal static float Distance(float radius, float zoom) =>
        2f * (1f + Math.Max(0f, radius) * Math.Max(0.1f, 1f + zoom));

    private static Vector3 Horizontal(Vector3 value) => new(value.x, 0f, value.z);
}
