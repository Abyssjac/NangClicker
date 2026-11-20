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
    public enum HexMotionControl { Free = 0, Holding = 1, Charging = 2 }

    /// <summary>One immutable cast and its independent contributions. Controlled lifts do not couple
    /// to neighbors until released. All free fields use the same solver in game and editor.</summary>
    public sealed class HexSpellWave
    {
        private sealed class Domain
        {
            public readonly List<HexCoordinate> cells = new List<HexCoordinate>();
            public readonly Dictionary<HexCoordinate, int> indices = new Dictionary<HexCoordinate, int>();
            public readonly int[,] neighbors;
            public readonly HexBoundarySource[,] boundary;
            public readonly List<Band>[] bands;
            public readonly List<HexBoundarySource> sources = new List<HexBoundarySource>();
            public readonly HexWaveSettings settings;
            public struct Band { public HexBoundarySource source; public float weight; }

            public Domain(HexSpellProperty spell, HexCoordinate origin, int direction, HexWaveSettings settings,
                Predicate<HexCoordinate> movable, Func<HexCoordinate, HexBoundarySource> mapBoundary, double start)
            {
                this.settings = settings;
                var mask = new HashSet<HexCoordinate>();
                foreach (var offset in spell.region) mask.Add(origin + offset.Rotate(direction));
                foreach (var cell in mask) if (movable(cell)) cells.Add(cell);
                cells.Sort();
                for (int i = 0; i < cells.Count; i++) indices.Add(cells[i], i);
                neighbors = new int[cells.Count, 6]; boundary = new HexBoundarySource[cells.Count, 6];
                bands = new List<Band>[cells.Count];
                var defaultSource = new HexBoundarySource(spell.Boundary, start);
                var fixedSource = new HexBoundarySource(HexBoundarySettings.Fixed, start, true);
                var overrides = new Dictionary<string, HexBoundarySource>();
                foreach (var edge in spell.boundaryEdges)
                    overrides.Add((origin + edge.offset.Rotate(direction)) + ":" + ((edge.direction + direction) % 6),
                        new HexBoundarySource(edge.response, start));
                var seeds = new Dictionary<HexBoundarySource, HashSet<int>>();
                for (int i = 0; i < cells.Count; i++)
                {
                    bands[i] = new List<Band>();
                    for (int d = 0; d < 6; d++)
                    {
                        var next = cells[i].GetNeighbor(d);
                        if (indices.TryGetValue(next, out int j)) { neighbors[i, d] = j; continue; }
                        neighbors[i, d] = -1;
                        HexBoundarySource source;
                        // Scene geometry takes precedence; a spell override never opens a real wall.
                        if (!movable(next)) source = mapBoundary?.Invoke(next) ?? fixedSource;
                        else if (!overrides.TryGetValue(cells[i] + ":" + d, out source)) source = defaultSource;
                        boundary[i, d] = source;
                        if (!seeds.TryGetValue(source, out var set)) { set = new HashSet<int>(); seeds.Add(source, set); sources.Add(source); }
                        set.Add(i);
                    }
                }
                foreach (var pair in seeds)
                {
                    int width = Mathf.Max(1, pair.Key.Settings.absorptionWidth);
                    var distances = new int[cells.Count];
                    for (int i = 0; i < distances.Length; i++) distances[i] = int.MaxValue;
                    var queue = new Queue<int>();
                    foreach (int i in pair.Value) { distances[i] = 0; queue.Enqueue(i); }
                    while (queue.Count > 0)
                    {
                        int i = queue.Dequeue();
                        bands[i].Add(new Band { source = pair.Key, weight = 1f - (float)distances[i] / width });
                        if (distances[i] + 1 >= width) continue;
                        for (int d = 0; d < 6; d++)
                        {
                            int j = neighbors[i, d];
                            if (j >= 0 && distances[j] > distances[i] + 1)
                            { distances[j] = distances[i] + 1; queue.Enqueue(j); }
                        }
                    }
                }
            }
            public bool FixedSupport(HexBoundarySource source, double time)
            {
                var mode = source.ModeAt(time);
                return mode == HexSpellEdgeMode.Fixed || mode == HexSpellEdgeMode.Amplify ||
                    (source.IsSceneWall && mode != HexSpellEdgeMode.Free);
            }
        }

        private sealed class Field
        {
            private readonly Domain domain;
            public float[] height, velocity;
            private float[] nextHeight, nextVelocity;
            private int quietSteps;
            public bool Sleeping { get; private set; }
            public Field(Domain domain)
            {
                this.domain = domain; int n = domain.cells.Count;
                height = new float[n]; velocity = new float[n]; nextHeight = new float[n]; nextVelocity = new float[n];
            }
            public void Integrate(float duration, double start)
            {
                var s = domain.settings;
                float highestDamping = s.cOrigin;
                foreach (var source in domain.sources) highestDamping = Mathf.Max(highestDamping, s.cOrigin + source.Settings.damping);
                float frequency = Mathf.Sqrt((s.kOrigin + 12f * s.kNeighbor) / s.mass);
                int steps = Mathf.Max(1, Mathf.Max(s.minimumSubsteps, Mathf.CeilToInt(duration * Mathf.Max(
                    frequency / 0.35f, (highestDamping + 12f * s.cNeighbor) / (0.5f * s.mass)))));
                float dt = duration / steps;
                for (int step = 0; step < steps; step++)
                {
                    double now = start + step * dt;
                    for (int i = 0; i < height.Length; i++)
                    {
                        float extraDamping = 0;
                        foreach (var band in domain.bands[i])
                            if (band.source.ModeAt(now) == HexSpellEdgeMode.Absorb)
                                extraDamping = Mathf.Max(extraDamping, band.source.Settings.damping * band.weight);
                        float force = -s.kOrigin * height[i] - (s.cOrigin + extraDamping) * velocity[i];
                        for (int d = 0; d < 6; d++)
                        {
                            int j = domain.neighbors[i, d];
                            if (j < 0 && !domain.FixedSupport(domain.boundary[i, d], now)) continue;
                            force += s.kNeighbor * ((j >= 0 ? height[j] : 0) - height[i]) +
                                s.cNeighbor * ((j >= 0 ? velocity[j] : 0) - velocity[i]);
                        }
                        nextVelocity[i] = velocity[i] + force / s.mass * dt;
                        nextHeight[i] = height[i] + nextVelocity[i] * dt;
                    }
                    // Pump after the shared snapshot calculation; one gate per adjacent cell/source.
                    for (int i = 0; i < height.Length; i++)
                        for (int d = 0; d < 6; d++)
                            if (domain.neighbors[i, d] < 0)
                            {
                                var source = domain.boundary[i, d];
                                bool seen = false;
                                for (int previous = 0; previous < d; previous++)
                                    if (ReferenceEquals(domain.boundary[i, previous], source)) { seen = true; break; }
                                if (!seen) source.TryBoost(domain.cells[i], nextHeight[i], ref nextVelocity[i], s.mass, now + dt);
                            }
                    (height, nextHeight) = (nextHeight, height); (velocity, nextVelocity) = (nextVelocity, velocity);
                }
            }
            public void CheckSleep()
            {
                var s = domain.settings; bool quiet = true;
                for (int i = 0; quiet && i < height.Length; i++)
                    quiet = Mathf.Abs(height[i]) <= s.sleepHeight && Mathf.Abs(velocity[i]) <= s.sleepVelocity;
                quietSteps = quiet ? quietSteps + 1 : 0;
                if (quietSteps >= s.sleepSteps)
                { Array.Clear(height, 0, height.Length); Array.Clear(velocity, 0, velocity.Length); Sleeping = true; }
            }
            public float Energy(double time)
            {
                var s = domain.settings; float total = 0;
                for (int i = 0; i < height.Length; i++)
                {
                    total += 0.5f * s.mass * velocity[i] * velocity[i] + 0.5f * s.kOrigin * height[i] * height[i];
                    for (int d = 0; d < 6; d++)
                    {
                        int j = domain.neighbors[i, d];
                        if (j >= 0 && j <= i) continue;
                        if (j < 0 && !domain.FixedSupport(domain.boundary[i, d], time)) continue;
                        float difference = height[i] - (j >= 0 ? height[j] : 0);
                        total += 0.5f * s.kNeighbor * difference * difference;
                    }
                }
                return total;
            }
        }

        private struct Scheduled
        {
            public int index;
            public double time;
            public HexSpellEventMode mode;
            public float value;
        }
        private struct Lift
        {
            public int index;
            public double start, release;
            public float height;
        }
        private readonly Domain domain;
        private readonly List<Scheduled> events = new List<Scheduled>();
        private readonly List<Lift> lifts = new List<Lift>();
        private readonly List<Field> fields = new List<Field>();
        private Field impulseField;
        private readonly double startTime;
        private readonly float chargeDuration;
        private double clock;
        private int nextEvent;
        public int CellCount => domain.cells.Count;
        public int PendingEventCount => events.Count - nextEvent + lifts.Count;
        public bool IsFinished { get; private set; }
        public double Time => clock;
        public int ControlledCellCount => lifts.Count;
        public float AmplificationEnergySpent { get { float result = 0; foreach (var s in domain.sources) result += s.EnergySpent; return result; } }
        public HexCoordinate GetCoordinate(int i) => domain.cells[i];
        public float GetHeight(int i)
        {
            float value = 0; foreach (var field in fields) value += field.height[i];
            foreach (var lift in lifts) if (lift.index == i) value += SampleLift(lift, false);
            return value;
        }
        public float GetVelocity(int i)
        {
            float value = 0; foreach (var field in fields) value += field.velocity[i];
            foreach (var lift in lifts) if (lift.index == i) value += SampleLift(lift, true);
            return value;
        }
        public float GetHeight(HexCoordinate cell) => domain.indices.TryGetValue(cell, out int i) ? GetHeight(i) : 0;
        public HexMotionControl GetControl(HexCoordinate cell)
        {
            if (!domain.indices.TryGetValue(cell, out int i)) return HexMotionControl.Free;
            var state = HexMotionControl.Free;
            foreach (var lift in lifts) if (lift.index == i)
                state = (HexMotionControl)Mathf.Max((int)state, (int)(clock < lift.start + chargeDuration ? HexMotionControl.Charging : HexMotionControl.Holding));
            return state;
        }
        private float SampleLift(Lift lift, bool velocity)
        {
            float u = Mathf.Clamp01((float)((clock - lift.start) / chargeDuration));
            if (velocity) return lift.height * 30f * u * u * (1f - u) * (1f - u) / chargeDuration;
            return lift.height * (u * u * u * (10f + u * (-15f + 6f * u)));
        }
        public float MechanicalEnergy
        {
            get
            {
                float total = 0; foreach (var field in fields) total += field.Energy(startTime + clock);
                foreach (var lift in lifts)
                {
                    float h = SampleLift(lift, false), v = SampleLift(lift, true);
                    float stiffness = domain.settings.kOrigin;
                    for (int d = 0; d < 6; d++)
                        if (domain.neighbors[lift.index, d] >= 0 || domain.FixedSupport(domain.boundary[lift.index, d], startTime + clock))
                            stiffness += domain.settings.kNeighbor;
                    total += 0.5f * domain.settings.mass * v * v + 0.5f * stiffness * h * h;
                }
                return total;
            }
        }

        public HexSpellWave(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier,
            HexWaveSettings settings, Predicate<HexCoordinate> movable,
            Func<HexCoordinate, HexBoundarySource> mapBoundary = null, double startTime = 0)
        {
            direction = (direction % 6 + 6) % 6;
            this.startTime = startTime; chargeDuration = Mathf.Max(0.01f, spell.chargeDuration);
            domain = new Domain(spell, origin, direction, settings, movable, mapBoundary, startTime);
            foreach (var hit in spell.impulses)
                if (domain.indices.TryGetValue(origin + hit.offset.Rotate(direction), out int index))
                    events.Add(new Scheduled { index = index, time = hit.time, mode = hit.mode,
                        value = hit.mode == HexSpellEventMode.Impulse ? hit.impulse * spell.strength * multiplier : spell.LiftHeight(hit, multiplier) });
            events.Sort((a, b) => a.time.CompareTo(b.time));
            holdDuration = spell.holdDuration;
            ProcessDue();
            IsFinished = CellCount == 0 || events.Count == 0;
        }
        private readonly float holdDuration;
        private void ProcessDue()
        {
            while (nextEvent < events.Count && events[nextEvent].time <= clock + 1e-8)
            {
                var hit = events[nextEvent++];
                if (hit.mode == HexSpellEventMode.Impulse)
                {
                    if (impulseField == null) { impulseField = new Field(domain); fields.Add(impulseField); }
                    impulseField.velocity[hit.index] += hit.value / domain.settings.mass;
                }
                else lifts.Add(new Lift { index = hit.index, start = hit.time, release = hit.time + chargeDuration + holdDuration, height = hit.value });
            }
            for (int i = lifts.Count - 1; i >= 0; i--)
                if (lifts[i].release <= clock + 1e-8)
                {
                    var field = new Field(domain);
                    field.height[lifts[i].index] = lifts[i].height; // Endpoints have exactly zero velocity: no extra impulse.
                    fields.Add(field); lifts.RemoveAt(i);
                }
        }
        public void Step(float deltaTime)
        {
            if (IsFinished || deltaTime <= 0) return;
            double end = clock + deltaTime;
            while (clock < end - 1e-10)
            {
                double stop = nextEvent < events.Count ? Math.Min(end, events[nextEvent].time) : end;
                foreach (var lift in lifts) stop = Math.Min(stop, lift.release);
                if (stop > clock) foreach (var field in fields) field.Integrate((float)(stop - clock), startTime + clock);
                clock = stop; ProcessDue();
            }
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                fields[i].CheckSleep();
                if (!fields[i].Sleeping) continue;
                if (ReferenceEquals(fields[i], impulseField)) impulseField = null;
                fields.RemoveAt(i);
            }
            IsFinished = PendingEventCount == 0 && fields.Count == 0;
        }
    }
}
