using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>Shared deterministic coordinator used by the game and editor. Wall contacts are evaluated
    /// once from the combined free-wave snapshot, then the same additive multiplier is applied to all contributions.</summary>
    public sealed class HexWaveWorld
    {
        private sealed class Lease
        {
            public HexBoundarySource source;
            public double start, end;
            public long order;
        }
        private readonly Predicate<HexCoordinate> baseMovable;
        private readonly Func<HexCoordinate, HexBoundarySource> sceneBoundary;
        private readonly Func<Key_HexBoundaryProfilePP, HexBoundaryProfileProperty> profiles;
        private readonly Dictionary<HexCoordinate, List<Lease>> wallLeases = new Dictionary<HexCoordinate, List<Lease>>();
        private readonly List<HexSpellWave> casts = new List<HexSpellWave>();
        private readonly Dictionary<HexCoordinate, Vector2> freeSnapshot = new Dictionary<HexCoordinate, Vector2>();
        private readonly List<HexCoordinate> expiredCells = new List<HexCoordinate>();
        private readonly HexBoundarySource fixedBoundary = new HexBoundarySource(HexBoundarySettings.Fixed);
        private long nextOrder;
        public double Time { get; private set; }
        public int WallVersion { get; private set; }
        public int ActiveWallCount => wallLeases.Count;
        public IReadOnlyList<HexSpellWave> Casts => casts;
        public bool IsIdle => casts.Count == 0 && wallLeases.Count == 0;
        public string LastError { get; private set; }
        public HexWaveWorld(Predicate<HexCoordinate> baseMovable, Func<HexCoordinate, HexBoundarySource> sceneBoundary,
            Func<Key_HexBoundaryProfilePP, HexBoundaryProfileProperty> profiles)
        { this.baseMovable = baseMovable; this.sceneBoundary = sceneBoundary; this.profiles = profiles; }
        public bool BaseMovable(HexCoordinate cell) => baseMovable(cell);
        public bool IsBlocked(HexCoordinate cell) => !baseMovable(cell) || wallLeases.ContainsKey(cell);
        public bool IsTemporaryWall(HexCoordinate cell) => baseMovable(cell) && wallLeases.ContainsKey(cell);
        internal HexBoundaryProfileProperty ResolveProfile(Key_HexBoundaryProfilePP key) => profiles?.Invoke(key);
        public bool CanBegin(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier = 1)
        {
            LastError = null;
            if (spell == null) { LastError = "No spell selected."; return false; }
            if (!spell.Validate(out string error)) { LastError = error; return false; }
            if (!HexSpellProperty.Finite(multiplier) || !HexSpellProperty.Finite(spell.strength * multiplier))
            { LastError = "Invalid cast strength."; return false; }
            if (IsBlocked(origin)) { LastError = "The anchor is blocked."; return false; }
            foreach (var e in spell.impulses)
            {
                if (IsBlocked(origin + e.offset.Rotate(direction)))
                { LastError = "An event cell overlaps a wall or is outside the map."; return false; }
                float value = e.mode == HexSpellEventMode.Impulse ? e.impulse * spell.strength * multiplier : spell.LiftHeight(e, multiplier);
                if (!HexSpellProperty.Finite(value)) { LastError = "An event exceeds the supported numeric range."; return false; }
            }
            if (spell.walls != null) foreach (var wall in spell.walls)
            {
                var profile = ResolveProfile(wall.response);
                if (profile == null || !profile.IsValid || profile.mode == HexSpellEdgeMode.Free)
                { LastError = "Missing or invalid boundary profile: " + wall.response; return false; }
            }
            return true;
        }
        public HexSpellWave BeginCast(HexSpellProperty spell, HexCoordinate origin, int direction,
            HexWaveSettings settings, float multiplier = 1, bool autoRelease = false)
        {
            if (!CanBegin(spell, origin, direction, multiplier)) return null;
            if (!HexSpellProperty.Finite(settings.mass) || settings.mass <= 0) { LastError = "Mass must be positive."; return null; }
            var cast = new HexSpellWave(this, spell, origin, direction, multiplier, settings, autoRelease);
            casts.Add(cast); return cast;
        }
        public bool ReleaseCast(HexSpellWave cast)
        {
            if (cast == null || !casts.Contains(cast)) return false;
            if (!cast.CanCommit()) { cast.Cancel(); return false; }
            cast.Commit();
            foreach (var wall in cast.Walls)
            {
                if (!baseMovable(wall.cell)) continue; // Authored geometry always wins.
                if (!wallLeases.TryGetValue(wall.cell, out var leases))
                { leases = new List<Lease>(); wallLeases.Add(wall.cell, leases); }
                leases.Add(new Lease { start = Time, end = Time + cast.WallDuration, order = nextOrder++,
                    source = new HexBoundarySource(wall.settings, true, wall.amplification) });
                WallVersion++;
            }
            RefreshTopology();
            cast.ProcessEvents();
            return true;
        }
        // Same-cell overlaps do not stack. Fixed > Absorb > Amplify; ties use latest creation.
        private static int Priority(HexSpellEdgeMode mode) => mode == HexSpellEdgeMode.Fixed ? 3 : mode == HexSpellEdgeMode.Absorb ? 2 : 1;
        private Lease Effective(List<Lease> leases)
        {
            Lease result = null;
            foreach (var lease in leases)
                if (result == null || Priority(lease.source.Settings.mode) > Priority(result.source.Settings.mode) ||
                    Priority(lease.source.Settings.mode) == Priority(result.source.Settings.mode) && lease.order > result.order)
                    result = lease;
            return result;
        }
        public HexBoundarySource GetWallSource(HexCoordinate cell)
        {
            if (!baseMovable(cell)) return sceneBoundary?.Invoke(cell) ?? fixedBoundary;
            return wallLeases.TryGetValue(cell, out var leases) ? Effective(leases).source : fixedBoundary;
        }
        public float WallRemaining01(HexCoordinate cell)
        {
            if (!wallLeases.TryGetValue(cell, out var leases)) return 1;
            var lease = Effective(leases);
            return Mathf.Clamp01((float)((lease.end - Time) / (lease.end - lease.start)));
        }
        public float WallFlash(HexCoordinate cell) => Mathf.Clamp01(1 - (float)(Time - GetWallSource(cell).LastTriggerTime) / 0.16f);
        public float ContactFlash(HexCoordinate wall, HexCoordinate contact)
        {
            var source = GetWallSource(wall);
            return source.ContactFlash(contact, Time);
        }
        public float Height(HexCoordinate cell) { float h = 0; foreach (var cast in casts) h += cast.GetHeight(cell); return h; }
        public float Velocity(HexCoordinate cell) { float v = 0; foreach (var cast in casts) v += cast.GetVelocity(cell); return v; }
        public HexMotionControl Control(HexCoordinate cell)
        {
            var control = HexMotionControl.Free;
            foreach (var cast in casts) control = (HexMotionControl)Mathf.Max((int)control, (int)cast.GetControl(cell));
            return control;
        }
        private void RefreshTopology() { foreach (var cast in casts) cast.RefreshTopology(); }
        private void ProcessBoundaries()
        {
            expiredCells.Clear();
            foreach (var pair in wallLeases)
            {
                if (pair.Value.RemoveAll(x => Time + 1e-8 >= x.end) > 0) WallVersion++;
                if (pair.Value.Count == 0) expiredCells.Add(pair.Key);
            }
            foreach (var c in expiredCells) wallLeases.Remove(c);
            RefreshTopology();
            foreach (var cast in casts)
            {
                if (cast.AutoRelease && cast.Phase == HexCastPhase.Ready) ReleaseCast(cast);
                cast.ProcessEvents();
            }
        }
        private void Amplify()
        {
            freeSnapshot.Clear();
            foreach (var cast in casts) cast.CollectFree(freeSnapshot);
            foreach (var pair in freeSnapshot)
            {
                float gain = 0;
                for (int d = 0; d < 6; d++)
                {
                    var wall = pair.Key.GetNeighbor(d);
                    if (IsBlocked(wall)) gain += GetWallSource(wall).ConsumeGain(pair.Key, pair.Value.x, pair.Value.y, Time);
                }
                if (gain > 0) foreach (var cast in casts) cast.Boost(pair.Key, 1 + gain);
            }
        }
        public void Step(float deltaTime)
        {
            if (!HexSpellProperty.Finite(deltaTime) || deltaTime <= 0) return;
            double end = Time + deltaTime;
            ProcessBoundaries();
            while (Time < end - 1e-8)
            {
                double stop = end;
                foreach (var pair in wallLeases) foreach (var lease in pair.Value) stop = Math.Min(stop, lease.end);
                foreach (var cast in casts)
                {
                    stop = Math.Min(stop, cast.NextBoundary());
                    var s = cast.Settings;
                    float frequency = Mathf.Sqrt((s.kOrigin + 12 * s.kNeighbor) / s.mass);
                    float rate = Mathf.Max(frequency / 0.35f, (cast.MaximumDamping + 12 * s.cNeighbor) / (0.5f * s.mass));
                    if (rate > 0) stop = Math.Min(stop, Time + 1.0 / rate);
                    stop = Math.Min(stop, Time + deltaTime / Mathf.Max(1, s.minimumSubsteps));
                }
                if (stop <= Time + 1e-8) stop = Math.Min(end, Time + 1e-6);
                float dt = (float)(stop - Time);
                foreach (var cast in casts) cast.Integrate(dt);
                Time = stop;
                foreach (var cast in casts) cast.AdvanceControl(dt);
                Amplify();
                ProcessBoundaries();
            }
            foreach (var cast in casts) cast.CheckSleep();
            casts.RemoveAll(x => x.IsFinished);
        }
        public void Reset()
        {
            foreach (var cast in casts) cast.Cancel();
            casts.Clear(); wallLeases.Clear(); freeSnapshot.Clear(); Time = 0; WallVersion++;
        }
    }
}
