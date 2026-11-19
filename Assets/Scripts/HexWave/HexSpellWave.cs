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

    /// <summary>Isolated linear wave contribution. No scene objects, asset mutation or player state.
    /// Used unchanged by the game and the editor preview. Missing/blocked cells are fixed supports;
    /// cells outside the spell mask never acquire a height or velocity.</summary>
    public sealed class HexSpellWave
    {
        private readonly Dictionary<HexCoordinate, int> indices = new Dictionary<HexCoordinate, int>();
        private readonly List<HexCoordinate> coordinates = new List<HexCoordinate>();
        private readonly List<HexSpellImpulse> events = new List<HexSpellImpulse>();
        private readonly int[,] neighbors;
        private readonly float[] damping;
        private float[] heights, velocities, nextHeights, nextVelocities;
        private readonly HexWaveSettings settings;
        private readonly float strength;
        private int nextEvent, quietSteps;
        private double clock;
        public int CellCount => coordinates.Count;
        public int PendingEventCount => events.Count - nextEvent;
        public bool IsFinished { get; private set; }
        public double Time => clock;
        public HexCoordinate GetCoordinate(int index) => coordinates[index];
        public float GetHeight(int index) => heights[index];
        public float GetVelocity(int index) => velocities[index];
        public float GetHeight(HexCoordinate coordinate) => indices.TryGetValue(coordinate, out int i) ? heights[i] : 0f;
        public float MechanicalEnergy
        {
            get
            {
                float energy = 0f;
                for (int i = 0; i < CellCount; i++)
                {
                    energy += 0.5f * settings.mass * velocities[i] * velocities[i] + 0.5f * settings.kOrigin * heights[i] * heights[i];
                    for (int d = 0; d < 6; d++)
                    {
                        int j = neighbors[i, d];
                        if (j == -2 || (j >= 0 && j <= i)) continue;
                        float difference = heights[i] - (j >= 0 ? heights[j] : 0f);
                        energy += 0.5f * settings.kNeighbor * difference * difference;
                    }
                }
                return energy;
            }
        }

        public HexSpellWave(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier,
            HexWaveSettings settings, Predicate<HexCoordinate> movable)
        {
            this.settings = settings;
            strength = spell.strength * multiplier;
            var mask = new HashSet<HexCoordinate>();
            foreach (var offset in spell.region) mask.Add(origin + offset.Rotate(direction));
            foreach (var coordinate in mask)
                if (movable(coordinate)) { indices.Add(coordinate, coordinates.Count); coordinates.Add(coordinate); }
            int count = coordinates.Count;
            neighbors = new int[count, 6];
            damping = new float[count];
            heights = new float[count]; velocities = new float[count];
            nextHeights = new float[count]; nextVelocities = new float[count];
            var distance = new int[count];
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                distance[i] = int.MaxValue;
                for (int d = 0; d < 6; d++)
                {
                    var neighbor = coordinates[i].GetNeighbor(d);
                    // -1: fixed scene support; -2: open spell edge. Neither transmits across a wall.
                    neighbors[i, d] = indices.TryGetValue(neighbor, out int j) ? j :
                        (!movable(neighbor) || spell.edgeMode == HexSpellEdgeMode.Fixed ? -1 : -2);
                    if (!mask.Contains(neighbor) && distance[i] != 0)
                    { distance[i] = 0; queue.Enqueue(i); }
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int j = neighbors[i, d];
                    if (j >= 0 && distance[j] > distance[i] + 1)
                    { distance[j] = distance[i] + 1; queue.Enqueue(j); }
                }
            }
            for (int i = 0; i < count; i++)
                damping[i] = settings.cOrigin + (spell.edgeMode == HexSpellEdgeMode.Absorb && distance[i] < spell.absorptionWidth
                    ? spell.edgeDamping * (1f - (float)distance[i] / spell.absorptionWidth) : 0f);
            foreach (var hit in spell.impulses)
            {
                var coordinate = origin + hit.offset.Rotate(direction);
                if (indices.ContainsKey(coordinate)) events.Add(new HexSpellImpulse(coordinate, hit.time, hit.impulse));
            }
            events.Sort((a, b) => a.time.CompareTo(b.time));
            ApplyDueEvents();
            IsFinished = events.Count == 0 || count == 0;
        }

        public void Step(float deltaTime)
        {
            if (IsFinished || deltaTime <= 0f) return;
            double end = clock + deltaTime;
            while (clock < end - 1e-10)
            {
                double stop = nextEvent < events.Count ? Math.Min(end, events[nextEvent].time) : end;
                if (stop > clock) Integrate((float)(stop - clock));
                clock = stop;
                ApplyDueEvents();
            }
            bool quiet = PendingEventCount == 0;
            for (int i = 0; quiet && i < CellCount; i++)
                quiet = Mathf.Abs(heights[i]) <= settings.sleepHeight && Mathf.Abs(velocities[i]) <= settings.sleepVelocity;
            quietSteps = quiet ? quietSteps + 1 : 0;
            if (quietSteps >= settings.sleepSteps)
            {
                Array.Clear(heights, 0, heights.Length); Array.Clear(velocities, 0, velocities.Length);
                IsFinished = true;
            }
        }

        private void ApplyDueEvents()
        {
            while (nextEvent < events.Count && events[nextEvent].time <= clock + 1e-8)
            {
                var hit = events[nextEvent++];
                velocities[indices[hit.offset]] += hit.impulse * strength / settings.mass;
                quietSteps = 0;
            }
        }

        private void Integrate(float deltaTime)
        {
            float highestDamping = settings.cOrigin;
            for (int i = 0; i < damping.Length; i++) highestDamping = Mathf.Max(highestDamping, damping[i]);
            float frequency = Mathf.Sqrt((settings.kOrigin + 12f * settings.kNeighbor) / settings.mass);
            int steps = Mathf.Max(settings.minimumSubsteps, Mathf.CeilToInt(deltaTime * Mathf.Max(
                frequency / 0.35f, (highestDamping + 12f * settings.cNeighbor) / (0.5f * settings.mass))));
            float dt = deltaTime / Mathf.Max(1, steps);
            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < CellCount; i++)
                {
                    float force = -settings.kOrigin * heights[i] - damping[i] * velocities[i];
                    for (int d = 0; d < 6; d++)
                    {
                        int j = neighbors[i, d];
                        if (j == -2) continue;
                        force += settings.kNeighbor * ((j >= 0 ? heights[j] : 0f) - heights[i]) +
                            settings.cNeighbor * ((j >= 0 ? velocities[j] : 0f) - velocities[i]);
                    }
                    nextVelocities[i] = velocities[i] + force / settings.mass * dt;
                    nextHeights[i] = heights[i] + nextVelocities[i] * dt;
                }
                (heights, nextHeights) = (nextHeights, heights);
                (velocities, nextVelocities) = (nextVelocities, velocities);
            }
        }
    }
}
