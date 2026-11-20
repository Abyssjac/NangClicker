using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [Serializable]
    public struct HexWaveSettings
    {
        public float mass, kOrigin, cOrigin, kNeighbor, cNeighbor;
        public float sleepHeight, sleepVelocity;
        public int sleepSteps, minimumSubsteps;
    }
    public enum HexMotionControl { Free = 0, Holding = 1, Charging = 2, Recovering = 3 }
    public enum HexCastPhase { Charging, Ready, Released, Cancelled, Finished }

    /// <summary>Immutable cast data and isolated contributions. Shared walls/boosts are coordinated by HexWaveWorld.</summary>
    public sealed class HexSpellWave
    {
        private sealed class Scheduled
        {
            public int index;
            public float delay, value;
            public HexSpellEventMode mode;
            public bool done;
        }
        private sealed class Recovery
        {
            public int index;
            public float height, velocity;
            public double start;
        }
        private sealed class Field
        {
            public float[] height, velocity, nextHeight, nextVelocity;
            public int quiet;
            public Field(int n) { height = new float[n]; velocity = new float[n]; nextHeight = new float[n]; nextVelocity = new float[n]; }
        }
        internal struct WallDefinition { public HexCoordinate cell; public HexBoundarySettings settings; public HexAmplifySettings amplification; }
        private readonly HexWaveWorld world;
        private readonly List<HexCoordinate> cells = new List<HexCoordinate>();
        private readonly Dictionary<HexCoordinate, int> indices = new Dictionary<HexCoordinate, int>();
        private readonly List<Scheduled> events = new List<Scheduled>();
        private readonly List<Field> fields = new List<Field>();
        private readonly List<Recovery> recoveries = new List<Recovery>();
        private readonly List<WallDefinition> walls = new List<WallDefinition>();
        private readonly HexBoundarySource defaultBoundary;
        private readonly HexWaveSettings settings;
        private readonly int[,] neighbors;
        private readonly HexBoundarySource[,] boundaries;
        private readonly bool[] blocked;
        private readonly float[] damping;
        private int wallVersion = -1;
        private float chargeElapsed;
        private double releasedAt;
        private Field impulseField;
        private const float RecoveryDuration = 0.16f;
        internal bool AutoRelease { get; }
        internal float WallDuration { get; }
        public HexCastPhase Phase { get; private set; } = HexCastPhase.Charging;
        public float ChargeDuration { get; }
        public float ChargeProgress => Mathf.Clamp01(chargeElapsed / ChargeDuration);
        public float ChargeElapsed => chargeElapsed;
        public HexCoordinate Origin { get; }
        public int Direction { get; }
        public int CellCount => cells.Count;
        public bool IsFinished => Phase == HexCastPhase.Finished;
        public int PendingEventCount { get { int count = 0; foreach (var e in events) if (!e.done) count++; return count; } }
        internal HexWaveSettings Settings => settings;
        internal IReadOnlyList<WallDefinition> Walls => walls;

        internal HexSpellWave(HexWaveWorld world, HexSpellProperty spell, HexCoordinate origin, int direction,
            float multiplier, HexWaveSettings settings, bool autoRelease)
        {
            this.world = world; this.settings = settings; Origin = origin; Direction = (direction % 6 + 6) % 6;
            ChargeDuration = spell.chargeDuration; WallDuration = spell.wallDuration; AutoRelease = autoRelease;
            defaultBoundary = new HexBoundarySource(spell.Boundary, false);
            foreach (var offset in spell.region)
            {
                var c = origin + offset.Rotate(Direction);
                if (world.BaseMovable(c)) cells.Add(c);
            }
            cells.Sort();
            for (int i = 0; i < cells.Count; i++) indices.Add(cells[i], i);
            neighbors = new int[cells.Count, 6]; boundaries = new HexBoundarySource[cells.Count, 6];
            blocked = new bool[cells.Count]; damping = new float[cells.Count];
            foreach (var hit in spell.impulses)
                if (indices.TryGetValue(origin + hit.offset.Rotate(Direction), out int i))
                    events.Add(new Scheduled { index = i, delay = hit.releaseDelay, mode = hit.mode,
                        value = hit.mode == HexSpellEventMode.Impulse ? hit.impulse * spell.strength * multiplier : spell.LiftHeight(hit, multiplier) });
            events.Sort((a, b) => a.delay.CompareTo(b.delay));
            if (spell.walls != null) foreach (var wall in spell.walls)
            {
                var profile = world.ResolveProfile(wall.response);
                walls.Add(new WallDefinition { cell = origin + wall.offset.Rotate(Direction),
                    settings = profile.Settings, amplification = profile.amplification });
            }
            RefreshTopology();
        }

        public HexCoordinate GetCoordinate(int i) => cells[i];
        public float GetHeight(int i)
        {
            float h = 0; foreach (var f in fields) h += f.height[i];
            foreach (var e in events) if (e.index == i && !e.done && e.mode == HexSpellEventMode.LiftAndRelease) h += LiftValue(e, false);
            foreach (var r in recoveries) if (r.index == i) h += RecoveryValue(r, false);
            return h;
        }
        public float GetVelocity(int i)
        {
            float v = 0; foreach (var f in fields) v += f.velocity[i];
            foreach (var e in events) if (e.index == i && !e.done && e.mode == HexSpellEventMode.LiftAndRelease) v += LiftValue(e, true);
            foreach (var r in recoveries) if (r.index == i) v += RecoveryValue(r, true);
            return v;
        }
        public float GetHeight(HexCoordinate cell) => indices.TryGetValue(cell, out int i) ? GetHeight(i) : 0;
        public float GetVelocity(HexCoordinate cell) => indices.TryGetValue(cell, out int i) ? GetVelocity(i) : 0;
        public HexMotionControl GetControl(HexCoordinate cell)
        {
            if (!indices.TryGetValue(cell, out int i)) return HexMotionControl.Free;
            foreach (var r in recoveries) if (r.index == i) return HexMotionControl.Recovering;
            foreach (var e in events) if (e.index == i && !e.done && e.mode == HexSpellEventMode.LiftAndRelease)
                return Phase == HexCastPhase.Charging ? HexMotionControl.Charging : HexMotionControl.Holding;
            return HexMotionControl.Free;
        }
        private float LiftValue(Scheduled e, bool velocity)
        {
            float u = ChargeProgress;
            if (velocity) return Phase == HexCastPhase.Charging ? e.value * 30 * u * u * (1 - u) * (1 - u) / ChargeDuration : 0;
            return e.value * u * u * u * (10 + u * (-15 + 6 * u));
        }
        private float RecoveryValue(Recovery r, bool velocity)
        {
            float u = Mathf.Clamp01((float)(world.Time - r.start) / RecoveryDuration);
            if (velocity) return r.height * (6 * u * u - 6 * u) / RecoveryDuration + r.velocity * (3 * u * u - 4 * u + 1);
            return r.height * (2 * u * u * u - 3 * u * u + 1) + r.velocity * RecoveryDuration * (u * u * u - 2 * u * u + u);
        }
        private void Recover(int i, float h, float v)
        {
            if (h != 0 || v != 0) recoveries.Add(new Recovery { index = i, height = h, velocity = v, start = world.Time });
        }
        public bool Cancel()
        {
            if (Phase != HexCastPhase.Charging && Phase != HexCastPhase.Ready) return false;
            foreach (var e in events)
            {
                if (!e.done && e.mode == HexSpellEventMode.LiftAndRelease) Recover(e.index, LiftValue(e, false), LiftValue(e, true));
                e.done = true;
            }
            Phase = HexCastPhase.Cancelled; return true;
        }
        internal bool CanCommit()
        {
            if (Phase != HexCastPhase.Ready) return false;
            if (world.IsBlocked(Origin)) return false;
            foreach (var e in events) if (world.IsBlocked(cells[e.index])) return false;
            return true;
        }
        internal void Commit()
        {
            Phase = HexCastPhase.Released; releasedAt = world.Time;
        }
        internal double NextBoundary()
        {
            double next = double.PositiveInfinity;
            if (Phase == HexCastPhase.Charging) next = world.Time + Math.Max(0, ChargeDuration - chargeElapsed);
            if (Phase == HexCastPhase.Released) foreach (var e in events) if (!e.done) next = Math.Min(next, releasedAt + e.delay);
            foreach (var r in recoveries) next = Math.Min(next, r.start + RecoveryDuration);
            return next;
        }
        internal void AdvanceControl(float dt)
        {
            if (Phase == HexCastPhase.Charging)
            {
                chargeElapsed = Mathf.Min(ChargeDuration, chargeElapsed + dt);
                if (chargeElapsed >= ChargeDuration - 1e-6f) { chargeElapsed = ChargeDuration; Phase = HexCastPhase.Ready; }
            }
            recoveries.RemoveAll(r => world.Time >= r.start + RecoveryDuration - 1e-8);
        }
        internal void ProcessEvents()
        {
            if (Phase != HexCastPhase.Released) return;
            foreach (var e in events)
            {
                if (e.done || world.Time + 1e-8 < releasedAt + e.delay) continue;
                e.done = true;
                if (blocked[e.index]) continue;
                if (e.mode == HexSpellEventMode.Impulse)
                {
                    if (impulseField == null) { impulseField = new Field(cells.Count); fields.Add(impulseField); }
                    impulseField.velocity[e.index] += e.value / settings.mass;
                }
                else
                {
                    var f = new Field(cells.Count); f.height[e.index] = e.value;
                    fields.Add(f); // Preserve target height and zero endpoint velocity; no additional impulse.
                }
            }
        }
        internal void RefreshTopology()
        {
            if (wallVersion == world.WallVersion) return;
            wallVersion = world.WallVersion;
            for (int i = 0; i < cells.Count; i++)
            {
                bool nowBlocked = world.IsBlocked(cells[i]);
                if (nowBlocked && !blocked[i])
                {
                    foreach (var f in fields) { Recover(i, f.height[i], f.velocity[i]); f.height[i] = f.velocity[i] = 0; }
                    foreach (var e in events) if (e.index == i && !e.done && e.mode == HexSpellEventMode.LiftAndRelease)
                    { Recover(i, LiftValue(e, false), LiftValue(e, true)); e.done = true; }
                }
                blocked[i] = nowBlocked; damping[i] = 0;
            }
            var seeds = new Dictionary<HexBoundarySource, HashSet<int>>();
            for (int i = 0; i < cells.Count; i++) for (int d = 0; d < 6; d++)
            {
                var next = cells[i].GetNeighbor(d);
                if (!blocked[i] && indices.TryGetValue(next, out int j) && !blocked[j])
                { neighbors[i, d] = j; boundaries[i, d] = null; continue; }
                neighbors[i, d] = -1;
                var source = world.IsBlocked(next) ? world.GetWallSource(next) : defaultBoundary;
                boundaries[i, d] = source;
                if (!blocked[i] && source.Settings.mode == HexSpellEdgeMode.Absorb)
                {
                    if (!seeds.TryGetValue(source, out var set)) { set = new HashSet<int>(); seeds.Add(source, set); }
                    set.Add(i);
                }
            }
            foreach (var pair in seeds)
            {
                int width = pair.Key.Settings.absorptionWidth;
                var distances = new int[cells.Count]; for (int i = 0; i < distances.Length; i++) distances[i] = -1;
                var queue = new Queue<int>();
                foreach (int i in pair.Value) { distances[i] = 0; queue.Enqueue(i); }
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    damping[i] = Mathf.Max(damping[i], pair.Key.Settings.damping * (1f - (float)distances[i] / width));
                    if (distances[i] + 1 >= width) continue;
                    for (int d = 0; d < 6; d++)
                    {
                        int j = neighbors[i, d]; if (j >= 0 && distances[j] < 0) { distances[j] = distances[i] + 1; queue.Enqueue(j); }
                    }
                }
            }
        }
        private bool FixedSupport(HexBoundarySource source) => source.IsSceneWall ||
            source.Settings.mode == HexSpellEdgeMode.Fixed || source.Settings.mode == HexSpellEdgeMode.Amplify;
        internal float MaximumDamping
        { get { float max = 0; foreach (float c in damping) max = Mathf.Max(max, c); return settings.cOrigin + max; } }
        internal void Integrate(float dt)
        {
            foreach (var f in fields)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (blocked[i]) { f.nextHeight[i] = f.nextVelocity[i] = 0; continue; }
                    float force = -settings.kOrigin * f.height[i] - (settings.cOrigin + damping[i]) * f.velocity[i];
                    for (int d = 0; d < 6; d++)
                    {
                        int j = neighbors[i, d];
                        if (j < 0 && !FixedSupport(boundaries[i, d])) continue;
                        force += settings.kNeighbor * ((j >= 0 ? f.height[j] : 0) - f.height[i]) +
                            settings.cNeighbor * ((j >= 0 ? f.velocity[j] : 0) - f.velocity[i]);
                    }
                    f.nextVelocity[i] = f.velocity[i] + force / settings.mass * dt;
                    f.nextHeight[i] = f.height[i] + f.nextVelocity[i] * dt;
                }
                (f.height, f.nextHeight) = (f.nextHeight, f.height);
                (f.velocity, f.nextVelocity) = (f.nextVelocity, f.velocity);
            }
        }
        internal void CollectFree(Dictionary<HexCoordinate, Vector2> snapshot)
        {
            for (int i = 0; i < cells.Count; i++) if (!blocked[i])
            {
                Vector2 value = Vector2.zero;
                foreach (var f in fields) value += new Vector2(f.height[i], f.velocity[i]);
                if (value == Vector2.zero) continue;
                snapshot.TryGetValue(cells[i], out var previous); snapshot[cells[i]] = previous + value;
            }
        }
        internal void Boost(HexCoordinate cell, float factor)
        {
            if (indices.TryGetValue(cell, out int i) && !blocked[i]) foreach (var f in fields) f.velocity[i] *= factor;
        }
        internal void CheckSleep()
        {
            for (int fIndex = fields.Count - 1; fIndex >= 0; fIndex--)
            {
                var f = fields[fIndex]; bool quiet = true;
                for (int i = 0; quiet && i < cells.Count; i++)
                    quiet = Mathf.Abs(f.height[i]) <= settings.sleepHeight && Mathf.Abs(f.velocity[i]) <= settings.sleepVelocity;
                f.quiet = quiet ? f.quiet + 1 : 0;
                if (f.quiet < settings.sleepSteps) continue;
                if (ReferenceEquals(f, impulseField)) impulseField = null;
                fields.RemoveAt(fIndex);
            }
            if ((Phase == HexCastPhase.Released || Phase == HexCastPhase.Cancelled) &&
                PendingEventCount == 0 && fields.Count == 0 && recoveries.Count == 0) Phase = HexCastPhase.Finished;
        }
        public float MechanicalEnergy
        {
            get
            {
                float energy = 0;
                foreach (var f in fields) for (int i = 0; i < cells.Count; i++)
                {
                    energy += 0.5f * settings.mass * f.velocity[i] * f.velocity[i] + 0.5f * settings.kOrigin * f.height[i] * f.height[i];
                    for (int d = 0; d < 6; d++)
                    {
                        int j = neighbors[i, d]; if (j >= 0 && j <= i || j < 0 && !FixedSupport(boundaries[i, d])) continue;
                        float delta = f.height[i] - (j >= 0 ? f.height[j] : 0); energy += 0.5f * settings.kNeighbor * delta * delta;
                    }
                }
                foreach (var e in events) if (!e.done && e.mode == HexSpellEventMode.LiftAndRelease)
                {
                    float h = LiftValue(e, false), v = LiftValue(e, true);
                    energy += 0.5f * settings.mass * v * v + 0.5f * (settings.kOrigin + 6 * settings.kNeighbor) * h * h;
                }
                return energy;
            }
        }
    }
}
