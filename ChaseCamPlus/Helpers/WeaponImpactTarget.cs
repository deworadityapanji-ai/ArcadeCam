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
    // Debris rigidbodies we switched to interpolation while watched, with their old setting.
    private readonly List<(Rigidbody Body, RigidbodyInterpolation Was)> _smoothed = new();
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

    /// <summary>Kill cam: follow this unit's wreck (largest surviving part) without a detonation.</summary>
    internal void Follow(Unit unit)
    {
        Watch(unit);
        _evidence.Force();
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
            point = Rendered(node.rb, node.transform);
            radius = node.IsDetached() || _unit == null ? Mathf.Max(3f, node.GetAverageRadius()) : _unit.maxRadius;
        }
        else
        {
            if (_unit == null || !HasMesh(_meshes, _unit.transform)) return false;
            point = Rendered(_unit.rb, _unit.transform);
            radius = _unit.maxRadius;
        }
        if (_unit != null) _unit.displayDetail = Mathf.Max(_unit.displayDetail, 1000f);
        return true;
    }

    /// <summary>The watched piece's velocity, for framing (zero when unknown).</summary>
    internal Vector3 Velocity()
    {
        Rigidbody body = _anchor?.Node != null ? _anchor.Node.rb : _unit != null ? _unit.rb : null;
        return body != null ? body.velocity : Vector3.zero;
    }

    // Where the piece is DRAWN this frame. Rigidbody.worldCenterOfMass is the physics pose, which
    // only moves on physics steps (about every third frame in slow motion) and made the view
    // judder against the smoothly drawn wreck. The transform is the interpolated, drawn pose;
    // debris without interpolation gets it switched on while it is watched.
    private Vector3 Rendered(Rigidbody body, Transform fallback)
    {
        if (body == null) return fallback.position;
        if (body.interpolation == RigidbodyInterpolation.None)
        {
            _smoothed.Add((body, body.interpolation));
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        return body.transform.TransformPoint(body.centerOfMass);
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
        foreach (var (body, was) in _smoothed) if (body != null) body.interpolation = was;
        _smoothed.Clear();
        _evidence.Reset();
    }
}
