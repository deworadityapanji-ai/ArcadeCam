using System.Collections.Generic;
using UnityEngine;

namespace ArcadeCam.Helpers;

// Keep references only to the tracked missile's own target and its existing parts.
// Never search nearby aircraft/debris or create, move or prolong wreckage.
internal sealed class WeaponImpactTarget
{
    private sealed class Part
    {
        internal UnitPart Node;
        internal Renderer[] Meshes;
    }

    private readonly List<Part> _parts = new();
    private readonly WeaponImpactEvidence _evidence = new();
    private Unit _unit;
    private Renderer[] _meshes;
    private Part _anchor;
    private bool _acquired;
    internal bool Confirmed => _evidence.Confirmed;

    internal void Watch(Unit unit)
    {
        if (ReferenceEquals(_unit, unit)) return;
        Reset();
        if (unit == null || unit is Missile) return;
        _unit = unit;
        _meshes = unit.GetComponentsInChildren<Renderer>(true);
        foreach (UnitPart node in unit.GetAllParts())
        {
            if (node == null) continue;
            _parts.Add(new Part { Node = node, Meshes = node.GetComponentsInChildren<Renderer>(true) });
            node.onApplyDamage += Damaged;
        }
    }

    internal void Detonate(Unit unit, Vector3 blast)
    {
        Watch(unit);
        bool nearby = unit != null && (unit.transform.position - blast).sqrMagnitude
            <= Mathf.Pow(Mathf.Max(30f, unit.maxRadius * 2f), 2f);
        _evidence.Detonate(Time.time, nearby);
    }

    private void Damaged(UnitPart.OnApplyDamage damage) =>
        _evidence.Damage(Time.time, damage.pierceDamage + damage.blastDamage);

    internal bool TryPoint(out Vector3 point, out float radius)
    {
        point = Vector3.zero; radius = 3f;
        if (!Confirmed) return false;
        if (!_acquired)
        {
            // Prefer a substantial surviving part, including a detached fuselage.
            // Keep that choice fixed so new fragments cannot steal the view.
            float mass = -1f;
            foreach (Part part in _parts)
                if (part.Node != null && HasMesh(part.Meshes, part.Node.transform) && part.Node.mass > mass)
                { _anchor = part; mass = part.Node.mass; }
            _acquired = true;
        }
        if (_anchor != null)
        {
            UnitPart node = _anchor.Node;
            if (node == null || !HasMesh(_anchor.Meshes, node.transform)) return false;
            point = node.rb != null ? node.rb.worldCenterOfMass : node.transform.position;
            radius = node.IsDetached() || _unit == null ? Mathf.Max(3f, node.GetAverageRadius()) : _unit.maxRadius;
        }
        else
        {
            if (_unit == null || !HasMesh(_meshes, _unit.transform)) return false;
            point = _unit.rb != null ? _unit.rb.worldCenterOfMass : _unit.transform.position;
            radius = _unit.maxRadius;
        }
        if (_unit != null) _unit.displayDetail = Mathf.Max(_unit.displayDetail, 1000f);
        return true;
    }

    private static bool HasMesh(Renderer[] meshes, Transform root)
    {
        if (!root.gameObject.activeInHierarchy || meshes == null) return false;
        foreach (Renderer mesh in meshes)
            if ((mesh is MeshRenderer || mesh is SkinnedMeshRenderer) && mesh.enabled && mesh.gameObject.activeInHierarchy
                && (mesh.transform == root || mesh.transform.IsChildOf(root))) return true;
        return false;
    }

    internal void Reset()
    {
        foreach (Part part in _parts)
            if (part.Node != null) part.Node.onApplyDamage -= Damaged;
        _parts.Clear(); _unit = null; _meshes = null; _anchor = null; _acquired = false;
        _evidence.Reset();
    }
}
