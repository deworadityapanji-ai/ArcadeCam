using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class TargetCameraFraming
{
    internal static Vector3 Offset(Vector3 targetDirection, Vector3 up, float distance)
    {
        if (targetDirection.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 direction = targetDirection.normalized;
        Vector3 lift = Vector3.ProjectOnPlane(up, direction);
        if (lift.sqrMagnitude < 0.0001f)
            lift = Vector3.ProjectOnPlane(Vector3.forward, direction);
        if (lift.sqrMagnitude < 0.0001f)
            lift = Vector3.ProjectOnPlane(Vector3.right, direction);

        // Stand opposite the target across the aircraft, slightly above the sight line.
        // A side/rear enemy therefore remains visible beyond our own aircraft.
        return -direction * distance + lift.normalized * (distance * 0.2f);
    }
}