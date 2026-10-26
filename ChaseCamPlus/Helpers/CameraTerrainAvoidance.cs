using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class CameraTerrainAvoidance
{
    internal static void Apply(CameraStateManager cam, Vector3 pivot)
    {
        Vector3 toPivot = pivot - cam.transform.position;
        if (toPivot.sqrMagnitude < 0.0001f)
            return;

        if (!Physics.Linecast(pivot, pivot - toPivot * 2f, out RaycastHit hit, PhysicsLayers.StaticsMask))
            return;

        float facing = Mathf.Max(Vector3.Dot(toPivot.normalized, hit.normal), 0.1f);
        Vector3 pulled = hit.point + toPivot.normalized / facing;
        if ((pulled - pivot).sqrMagnitude < toPivot.sqrMagnitude)
            cam.transform.position = pulled;
    }
}