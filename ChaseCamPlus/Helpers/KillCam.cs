using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ArcadeCam.Helpers;

/// <summary>
/// Kill cam, variant B: on a notable kill, the chase camera swings round to watch the wreck from
/// your own aircraft (re-using Look at Target's framing and smooth return), with a short slow
/// motion in single player. You keep full control of the aircraft throughout.
///
/// Also produces the Arcade HUD's DESTROYED / HIT / MISS callouts for your own kills and missiles.
///
/// Kills come from the game's kill-feed message (killer, victim, victim type). It does not say
/// which weapon was used, so a gun kill is inferred: the gun fired in the last couple of seconds
/// and none of your missiles ended at that target just before.
/// </summary>
internal static class KillCam
{
    private static readonly FieldInfo MissileTarget = AccessTools.Field(typeof(Missile), "target");
    private static readonly System.Random Dice = new();

    // Your aircraft and what its missiles were doing.
    private static Aircraft _owner;
    private static readonly Dictionary<Missile, bool> Detonations = new();        // missile -> blew up at its target
    private static readonly Dictionary<Unit, float> MissileEnded = new();         // target -> unscaled time
    private sealed class PendingCheck { internal Missile Missile; internal Unit Target; internal string Name; internal float At; }
    private static readonly List<PendingCheck> Pending = new();
    // Aircraft whose kill cam was already decided at the missile hit (unit -> unscaled time).
    private static readonly Dictionary<Unit, float> HitRolled = new();
    private static float _lastKillTime = -100f, _cooldownUntil;

    // The running kill cam.
    private static bool _active;
    private static float _start, _duration;
    private static Unit _victim;
    private static Rigidbody _victimRb;
    private static GlobalPosition _lastPosition;
    private static Vector3 _lastVelocity;
    private static float _lastSeen;
    private static bool _ownsTime, _cut, _always;
    private static float _setScale;

    internal static bool Active => _active;

    // ---- setup ------------------------------------------------------------------------------

    /// <summary>Observes missile detonations (same native RPC the weapon camera watches).</summary>
    internal static void Initialize(Harmony harmony)
    {
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Missile)))
        {
            if (!method.Name.StartsWith("UserCode_RpcDetonate_", StringComparison.Ordinal)) continue;
            var args = method.GetParameters();
            if (args.Length != 7 || args[0].ParameterType != typeof(Unit) || args[2].ParameterType != typeof(Vector3)) continue;
            try { harmony.Patch(method, prefix: new HarmonyMethod(typeof(KillCam), nameof(ObserveDetonation))); }
            catch (Exception error) { Plugin.Logger.LogWarning("Kill cam: HIT/MISS detection unavailable: " + error.Message); }
        }
    }

    // Judged when it happens: the game removes the missile before it reports the detonation, so
    // by the time the missile is "gone" the target may have moved on.
    private static void ObserveDetonation(Missile __instance, Unit __0, Vector3 __2)
    {
        if (__instance == null || _owner == null || __instance.owner != _owner) return;
        Unit target = MissileTarget?.GetValue(__instance) as Unit;
        if (target == null) { Detonations[__instance] = false; return; }
        Vector3 point = __0 != null ? __0.transform.TransformPoint(__2) : __2 + Datum.origin.position;
        float radius = Mathf.Max(30f, target.maxRadius * 1.5f);
        bool near = __0 == target || (point - target.transform.position).sqrMagnitude < radius * radius;
        Detonations[__instance] = near;
        // Kill cam at the hit itself, while the aircraft is still mostly whole and just starting to
        // break apart. The game only declares it dead later (once a critical part fails), which is
        // too late for a good shot. One roll per aircraft; its later kill message only announces.
        if (near && Plugin.KillCamOnMissileHit.Value && target is Aircraft && !target.disabled
            && !(HitRolled.TryGetValue(target, out float rolled) && Time.unscaledTime - rolled < 20f))
        {
            HitRolled[target] = Time.unscaledTime;
            HandleKill(target, UnitName(target), target.definition, KillKind.Aircraft, "missile hit", announce: false);
        }
    }

    private static void BindOwner(Aircraft aircraft)
    {
        if (_owner == aircraft) return;
        if (_owner != null) _owner.onDeregisterMissile -= MissileGone;
        _owner = aircraft;
        Detonations.Clear(); MissileEnded.Clear(); Pending.Clear(); HitRolled.Clear();
        if (_owner != null) _owner.onDeregisterMissile += MissileGone;
    }

    // A missile of yours has ended. Decide later whether it was a kill, a hit or a miss.
    private static void MissileGone(Missile missile)
    {
        if (missile == null) return;
        Unit target = MissileTarget?.GetValue(missile) as Unit;
        if (target == null || target == _owner) { Detonations.Remove(missile); return; }
        MissileEnded[target] = Time.unscaledTime;
        // Decide shortly after, once the detonation report has arrived.
        Pending.Add(new PendingCheck { Missile = missile, Target = target, Name = UnitName(target), At = Time.unscaledTime + 1.2f });
    }

    // ---- kills ------------------------------------------------------------------------------

    /// <summary>Called from the game's kill-feed message, on every client.</summary>
    internal static void OnKill(PersistentID killer, PersistentID killed, int killedType)
    {
        try
        {
            if (!Plugin.IsActive || _owner == null || _owner.disabled || killer != _owner.persistentID) return;
            UnitRegistry.TryGetPersistentUnit(killed, out PersistentUnit victim);
            Unit unit = victim?.unit;
            string name = victim != null && !string.IsNullOrEmpty(victim.unitName) ? victim.unitName : UnitName(unit);
            bool decided = unit != null && HitRolled.TryGetValue(unit, out float rolled) && Time.unscaledTime - rolled < 20f;
            if (decided) HitRolled.Remove(unit);
            HandleKill(unit, name, victim?.definition, (KillKind)killedType, "kill message", roll: !decided);
        }
        catch (Exception error) { Plugin.Logger.LogWarning("Kill cam: " + error.Message); }
    }

    /// <param name="announce">A confirmed kill: DESTROYED callout, multi-kill timing, no HIT/MISS.</param>
    /// <param name="roll">Consider playing the kill cam (false when it was decided at the missile hit).</param>
    private static void HandleKill(Unit unit, string name, UnitDefinition def, KillKind kind, string source,
        bool announce = true, bool roll = true)
    {
        try
        {
            float now = Time.unscaledTime;
            bool multi = now - _lastKillTime < 10f;
            if (announce)
            {
                _lastKillTime = now;
                // It is dead, so no HIT/MISS for it.
                Pending.RemoveAll(p => p.Target == unit);
                if (kind != KillKind.Missile) ArcadeCamApi.RaiseCallout("DESTROYED", name, alert: true);
            }
            if (!roll)
            {
                Plugin.Logger.LogInfo($"[KillCam] {name} destroyed (kill cam was decided at the missile hit)");
                return;
            }

            bool missile = !announce
                || (unit != null && MissileEnded.TryGetValue(unit, out float ended) && now - ended < 2f);
            bool gun = !missile && GunFiredRecently(_owner, 2.5f);
            float ownValue = _owner.definition != null ? _owner.definition.value : 0f;
            float ratio = ownValue > 0f && def != null ? def.value / ownValue : 1f;
            float distance = unit != null ? Vector3.Distance(unit.transform.position, _owner.transform.position) : 99999f;
            int score = KillCamScore.Score(kind, gun, ratio, distance, multi);
            KillCamFrequency frequency = Plugin.KillCamFrequency.Value;
            float chance = KillCamScore.Chance(score, frequency);

            string result;
            if (!KillCamScore.Eligible(kind, Plugin.KillCamIncludeGround.Value)) result = "not eligible";
            else if (frequency == KillCamFrequency.Off) result = "off";
            else if (_active) result = "kill cam already playing";
            else if (now < _cooldownUntil && score < KillCamScore.Guaranteed) result = $"cooldown {_cooldownUntil - now:F0}s";
            else if (Dice.NextDouble() >= chance) result = "chance roll lost";
            else result = Gate(unit, distance, frequency == KillCamFrequency.Always) ?? Begin(unit, score, frequency);
            Plugin.Logger.LogInfo($"[KillCam] {name} ({kind}, from {source}) score {score} [gun={gun} value x{ratio:F1} {distance:F0}m multi={multi}]"
                + $" chance {chance:P0} -> {result}");
        }
        catch (Exception error) { Plugin.Logger.LogWarning("Kill cam: " + error.Message); }
    }

    private static bool GunFiredRecently(Aircraft aircraft, float seconds)
    {
        if (aircraft.weaponStations == null) return false;
        foreach (WeaponStation s in aircraft.weaponStations)
            if (s != null && s.WeaponInfo != null && s.WeaponInfo.gun
                && Time.timeSinceLevelLoad - s.LastFiredTime < seconds && s.LastFiredTime > 0f)
                return true;
        return false;
    }

    /// <summary>
    /// Why the kill cam must not play right now, or null when it may. Always skips the comfort
    /// gates (distance, altitude, gear, descent, angle of attack) but never the two that would get
    /// you killed: a missile inbound, or the ground about to arrive.
    /// </summary>
    private static string Gate(Unit victim, float distance, bool always)
    {
        CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
        if (cam == null || cam.currentState != cam.chaseState || cam.followingUnit != _owner) return "not in chase view";
        if (WeaponCamera.IsDisplaying) return "weapon camera active";
        if (victim == null) return "victim not found";
        if (always) return Danger(true);
        if (distance > 12000f) return "too far to see";
        if (_owner.radarAlt < 150f) return "too low";
        if (_owner.gearState != LandingGear.GearState.LockedRetracted) return "gear down";
        float climb = Vector3.Dot(_owner.CockpitRB().velocity, Vector3.up);
        if (climb < -5f && _owner.radarAlt / -climb < 6f) return "descending toward the ground";
        if (Danger(false) is string danger) return danger;
        return null;
    }

    /// <summary>Things that end a running kill cam at once (and block a new one).</summary>
    private static string Danger(bool always)
    {
        MissileWarning warning = _owner.GetMissileWarningSystem();
        if (warning != null && warning.knownMissiles != null && warning.knownMissiles.Count > 0) return "missile inbound";
        float climb = Vector3.Dot(_owner.CockpitRB().velocity, Vector3.up);
        if (climb < -5f && _owner.radarAlt < 1500f && _owner.radarAlt / -climb < 5f) return "pull up";
        if (!always && _owner.cockpit != null && _owner.cockpit.rb != null && _owner.speed > 10f)
        {
            Vector3 v = _owner.cockpit.transform.InverseTransformDirection(_owner.cockpit.rb.velocity);
            if (Mathf.Atan2(v.y, v.z) * -Mathf.Rad2Deg > 18f) return "high angle of attack";
        }
        return null;
    }

    private static string Begin(Unit victim, int score, KillCamFrequency frequency)
    {
        _active = true;
        _start = Time.unscaledTime;
        _duration = Mathf.Clamp(Plugin.KillCamSeconds.Value, 0.5f, 6f);
        _victim = victim;
        _victimRb = victim != null ? victim.GetComponent<Rigidbody>() : null;
        _lastPosition = victim.transform.position.ToGlobalPosition();
        _lastVelocity = _victimRb != null ? _victimRb.velocity : Vector3.zero;
        _lastSeen = Time.time;
        _cooldownUntil = Time.unscaledTime + KillCamScore.Cooldown(frequency, score);
        _always = frequency == KillCamFrequency.Always;
        // Variant A cuts to the wreck (rendering only, like the weapon camera); B watches from your
        // aircraft. A falls back to B when the wreck has nothing left to show.
        _cut = KillCamScore.CutsToWreck(Plugin.KillCamView.Value, score)
               && WeaponCamera.StartKillView(SceneSingleton<CameraStateManager>.i, victim, _duration * 4f);
        // Slow motion only offline, never over the game's own slow motion or the pause screen.
        _ownsTime = GameManager.gameState == GameState.SinglePlayer && !GameplayUI.GameSlowMotion
                    && !GameplayUI.GameIsPaused && Plugin.KillCamSlowMotion.Value < 0.999f;
        _setScale = Time.timeScale;
        KillCamSound.Play();
        return "PLAYING " + (_cut ? "cut to wreck" : "from aircraft")
            + (_ownsTime ? $" with slow motion x{Plugin.KillCamSlowMotion.Value:F2}" : "");
    }

    private static void End(string why)
    {
        if (!_active) return;
        _active = false;
        if (_cut) WeaponCamera.EndKillView();
        _cut = false;
        _victim = null; _victimRb = null;
        ReleaseTime();
        if (why != null) Plugin.Logger.LogInfo("[KillCam] ended: " + why);
    }

    private static void ReleaseTime()
    {
        // Only undo our own change: if the game paused or someone else set the scale, leave it.
        if (_ownsTime && !GameplayUI.GameIsPaused && Mathf.Abs(Time.timeScale - _setScale) < 0.001f)
            Time.timeScale = 1f;
        _ownsTime = false;
    }

    // ---- per frame --------------------------------------------------------------------------

    internal static void Tick(CameraStateManager cam)
    {
        Aircraft aircraft = null;
        if (Plugin.IsActive) GameManager.GetLocalAircraft(out aircraft);
        if (aircraft != null && (aircraft.disabled || aircraft.HasEjected())) aircraft = null;
        BindOwner(aircraft);
        CheckPending();
        if (!_active) return;
        if (_cut && !WeaponCamera.KillViewActive) { End("wreck view finished"); return; }

        if (_owner == null) { End("aircraft lost"); return; }
        if (cam == null || cam.currentState != cam.chaseState) { End("left chase view"); return; }
        if (Danger(_always) is string danger) { End(danger); return; }
        var input = GameManager.playerInput;
        if (input != null && Time.unscaledTime - _start > 0.2f
            && (input.GetButtonDown("Fire") || input.GetButtonDown("Center"))) { End("skipped"); return; }
        float elapsed = Time.unscaledTime - _start;
        if (elapsed >= _duration) { End(null); return; }

        if (_ownsTime)
        {
            if (GameplayUI.GameIsPaused || GameplayUI.GameSlowMotion) { _ownsTime = false; return; }
            if (Mathf.Abs(Time.timeScale - _setScale) > 0.001f) { _ownsTime = false; return; }  // someone else changed it
            // Ease in over 0.15 s and back out over the last 0.35 s.
            float into = Mathf.Clamp01(elapsed / 0.15f), outOf = Mathf.Clamp01((_duration - elapsed) / 0.35f);
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(into, outOf));
            _setScale = Mathf.Lerp(1f, Mathf.Clamp(Plugin.KillCamSlowMotion.Value, 0.05f, 1f), weight);
            Time.timeScale = _setScale;
        }
    }

    // HIT / MISS callouts once a missile's result is known (and it was not a kill).
    private static void CheckPending()
    {
        if (Pending.Count == 0) return;
        float now = Time.unscaledTime;
        if (HitRolled.Count > 0)
            foreach (Unit u in new List<Unit>(HitRolled.Keys))
                if (u == null || now - HitRolled[u] > 30f) HitRolled.Remove(u);
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            PendingCheck p = Pending[i];
            if (now < p.At) continue;
            Pending.RemoveAt(i);
            bool hit = p.Missile != null && Detonations.TryGetValue(p.Missile, out bool near) && near;
            if (p.Missile != null) Detonations.Remove(p.Missile);
            if (p.Target == null || p.Target.disabled) continue;   // destroyed (by you or anyone)
            // A hit target may still die seconds later (the game waits for a critical part);
            // DESTROYED then follows HIT, as in Ace Combat.
            ArcadeCamApi.RaiseCallout(hit ? "HIT" : "MISS", p.Name, alert: false);
        }
    }

    /// <summary>Where the camera should look while the kill cam runs (used by Look at Target).</summary>
    internal static bool TryGetFocus(CameraStateManager cam, out Vector3 position)
    {
        position = Vector3.zero;
        if (!_active || _cut || cam == null || cam.followingUnit != _owner) return false;
        if (_victim != null && _victim.gameObject.activeInHierarchy)
        {
            position = _victim.transform.position;
            _lastPosition = position.ToGlobalPosition();
            if (_victimRb != null) _lastVelocity = _victimRb.velocity;
            _lastSeen = Time.time;
        }
        else
        {
            // The wreck despawned: keep watching where it was heading.
            position = _lastPosition.ToLocalPosition() + _lastVelocity * (Time.time - _lastSeen);
        }
        return true;
    }

    private static string UnitName(Unit unit) =>
        unit != null && unit.definition != null && !string.IsNullOrEmpty(unit.definition.unitName)
            ? unit.definition.unitName : "TARGET";

    internal static void Dispose()
    {
        End(null);
        BindOwner(null);
    }
}
