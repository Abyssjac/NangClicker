using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class HexWaveManager : MonoBehaviour
    {
        [Serializable]
        private readonly struct Edge
        {
            public int First { get; }
            public int Second { get; }

            public Edge(int first, int second)
            {
                First = first;
                Second = second;
            }
        }

        [Header("Grid Authoring")]
        [SerializeField] private Transform cellsRoot;
        [SerializeField] private HexCellView cellPrefab;
        [Min(0.001f)] [SerializeField] private float spacing = 1.1f;

        [Header("Mass-Spring-Damper")]
        [Min(0.0001f)] [SerializeField] private float mass = 1f;
        [Min(0f)] [SerializeField] private float kOrigin = 20f;
        [Min(0f)] [SerializeField] private float cOrigin = 1.8f;
        [Min(0f)] [SerializeField] private float kNeighbor = 35f;
        [Min(0f)] [SerializeField] private float cNeighbor = 0.6f;
        [Range(1, 16)] [SerializeField] private int physicsSubsteps = 1;

        [Header("Impulse")]
        [SerializeField] private float defaultImpulse = 8f;
        [SerializeField] private List<HexImpulseRing> impulseProfile = new List<HexImpulseRing>
        {
            new HexImpulseRing(0, 1f)
        };

        [Header("Sleep")]
        [Min(0f)] [SerializeField] private float sleepHeightThreshold = 0.0005f;
        [Min(0f)] [SerializeField] private float sleepVelocityThreshold = 0.0005f;
        [Min(1)] [SerializeField] private int sleepSteps = 12;

        [Header("Debug")]
        [SerializeField] private bool drawBoundaryGizmos = true;

        private readonly Dictionary<HexCoordinate, int> coordinateToIndex =
            new Dictionary<HexCoordinate, int>();
        private readonly List<Edge> edges = new List<Edge>();
        private readonly Queue<HexImpulseRequest> pendingImpulses = new Queue<HexImpulseRequest>();

        private HexCellView[] cells = Array.Empty<HexCellView>();
        private int[,] neighbors = new int[0, 0];
        private bool[] fixedBoundary = Array.Empty<bool>();
        private bool[] sleeping = Array.Empty<bool>();
        private int[] quietStepCounts = Array.Empty<int>();
        private float[] heights = Array.Empty<float>();
        private float[] previousHeights = Array.Empty<float>();
        private float[] velocities = Array.Empty<float>();
        private float[] nextHeights = Array.Empty<float>();
        private float[] nextVelocities = Array.Empty<float>();
        private float[] forces = Array.Empty<float>();
        private bool initialized;
        private int topologyVersion;

        public event Action<HexWaveManager> TopologyRebuilt;

        public Transform CellsRoot => cellsRoot != null ? cellsRoot : transform;
        public HexCellView CellPrefab => cellPrefab;
        public float Spacing => spacing;
        public float DefaultImpulse => defaultImpulse;
        public int CellCount => cells.Length;
        public bool IsInitialized => initialized;
        public int TopologyVersion => topologyVersion;

        private void Awake()
        {
            RebuildGrid();
        }

        private void FixedUpdate()
        {
            if (!initialized && !RebuildGrid())
                return;

            if (cells.Length == 0)
                return;

            Array.Copy(heights, previousHeights, heights.Length);
            ConsumePendingImpulses();

            float substepDeltaTime = Time.fixedDeltaTime / physicsSubsteps;
            for (int step = 0; step < physicsSubsteps; step++)
                SimulateSubstep(substepDeltaTime);
        }

        private void OnValidate()
        {
            mass = Mathf.Max(0.0001f, mass);
            spacing = Mathf.Max(0.001f, spacing);
            kOrigin = Mathf.Max(0f, kOrigin);
            cOrigin = Mathf.Max(0f, cOrigin);
            kNeighbor = Mathf.Max(0f, kNeighbor);
            cNeighbor = Mathf.Max(0f, cNeighbor);
            physicsSubsteps = Mathf.Clamp(physicsSubsteps, 1, 16);
            sleepHeightThreshold = Mathf.Max(0f, sleepHeightThreshold);
            sleepVelocityThreshold = Mathf.Max(0f, sleepVelocityThreshold);
            sleepSteps = Mathf.Max(1, sleepSteps);

            if (impulseProfile == null)
                impulseProfile = new List<HexImpulseRing>();

            if (impulseProfile.Count == 0)
                impulseProfile.Add(new HexImpulseRing(0, 1f));
        }

        public void ConfigureAuthoring(HexCellView prefab, Transform root, float cellSpacing)
        {
            cellPrefab = prefab;
            cellsRoot = root;
            spacing = Mathf.Max(0.001f, cellSpacing);
        }

        public bool RebuildGrid()
        {
            Transform root = CellsRoot;
            HexCellView[] discoveredCells = root.GetComponentsInChildren<HexCellView>(true);
            Array.Sort(discoveredCells, (left, right) => left.Coordinate.CompareTo(right.Coordinate));

            coordinateToIndex.Clear();
            edges.Clear();

            for (int i = 0; i < discoveredCells.Length; i++)
            {
                HexCellView cell = discoveredCells[i];
                if (!cell.Coordinate.IsValid)
                {
                    Debug.LogError($"[{nameof(HexWaveManager)}] Cell '{cell.name}' has invalid coordinate {cell.Coordinate}.", cell);
                    initialized = false;
                    return false;
                }

                if (!coordinateToIndex.TryAdd(cell.Coordinate, i))
                {
                    Debug.LogError($"[{nameof(HexWaveManager)}] Duplicate coordinate {cell.Coordinate}.", cell);
                    initialized = false;
                    return false;
                }
            }

            cells = discoveredCells;
            int count = cells.Length;
            neighbors = new int[count, 6];
            fixedBoundary = new bool[count];
            sleeping = new bool[count];
            quietStepCounts = new int[count];
            heights = new float[count];
            previousHeights = new float[count];
            velocities = new float[count];
            nextHeights = new float[count];
            nextVelocities = new float[count];
            forces = new float[count];

            for (int i = 0; i < count; i++)
            {
                bool boundary = false;
                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoordinate neighborCoordinate = cells[i].Coordinate.GetNeighbor(direction);
                    if (coordinateToIndex.TryGetValue(neighborCoordinate, out int neighborIndex))
                    {
                        neighbors[i, direction] = neighborIndex;
                        if (i < neighborIndex)
                            edges.Add(new Edge(i, neighborIndex));
                    }
                    else
                    {
                        neighbors[i, direction] = -1;
                        boundary = true;
                    }
                }

                fixedBoundary[i] = boundary;
                sleeping[i] = boundary;
                if (!Application.isPlaying || !initialized)
                    cells[i].CaptureBaseLocalPosition();
                cells[i].SetRuntimeBinding(i, boundary);
                cells[i].ApplyHeight(0f);
            }

            pendingImpulses.Clear();
            initialized = true;
            topologyVersion++;
            TopologyRebuilt?.Invoke(this);
            return true;
        }

        public void ResetSimulation()
        {
            if (!initialized)
                return;

            Array.Clear(heights, 0, heights.Length);
            Array.Clear(previousHeights, 0, previousHeights.Length);
            Array.Clear(velocities, 0, velocities.Length);
            Array.Clear(nextHeights, 0, nextHeights.Length);
            Array.Clear(nextVelocities, 0, nextVelocities.Length);
            Array.Clear(forces, 0, forces.Length);
            Array.Clear(quietStepCounts, 0, quietStepCounts.Length);
            pendingImpulses.Clear();

            for (int i = 0; i < cells.Length; i++)
            {
                sleeping[i] = fixedBoundary[i];
                cells[i].ApplyHeight(0f);
            }
        }

        public bool EnqueueImpulse(HexCoordinate center, float totalImpulse)
        {
            if (!initialized && !RebuildGrid())
                return false;

            if (!coordinateToIndex.TryGetValue(center, out int centerIndex) || fixedBoundary[centerIndex])
                return false;

            if (Mathf.Approximately(totalImpulse, 0f))
                return false;

            pendingImpulses.Enqueue(new HexImpulseRequest(center, totalImpulse));
            return true;
        }

        public bool EnqueueDefaultImpulse(HexCoordinate center)
        {
            return EnqueueImpulse(center, defaultImpulse);
        }

        public bool TryGetCell(HexCoordinate coordinate, out HexCellView cell)
        {
            if (coordinateToIndex.TryGetValue(coordinate, out int index) && index >= 0 && index < cells.Length)
            {
                cell = cells[index];
                return true;
            }

            cell = null;
            return false;
        }

        public bool TryGetCellIndex(HexCoordinate coordinate, out int index)
        {
            return coordinateToIndex.TryGetValue(coordinate, out index);
        }

        public HexCellView GetCellView(int index)
        {
            return index >= 0 && index < cells.Length ? cells[index] : null;
        }

        public float GetCurrentHeight(int index)
        {
            return index >= 0 && index < heights.Length ? heights[index] : 0f;
        }

        public float GetPreviousHeight(int index)
        {
            return index >= 0 && index < previousHeights.Length ? previousHeights[index] : 0f;
        }

        public float GetVelocity(int index)
        {
            return index >= 0 && index < velocities.Length ? velocities[index] : 0f;
        }

        public bool IsFixedBoundary(int index)
        {
            return index >= 0 && index < fixedBoundary.Length && fixedBoundary[index];
        }

        public bool IsSleeping(int index)
        {
            return index >= 0 && index < sleeping.Length && sleeping[index];
        }

        public float GetTotalMechanicalEnergy()
        {
            if (!initialized)
                return 0f;

            float energy = 0f;
            for (int i = 0; i < cells.Length; i++)
            {
                if (fixedBoundary[i])
                    continue;

                energy += 0.5f * mass * velocities[i] * velocities[i];
                energy += 0.5f * kOrigin * heights[i] * heights[i];
            }

            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                Edge edge = edges[edgeIndex];
                float difference = heights[edge.First] - heights[edge.Second];
                energy += 0.5f * kNeighbor * difference * difference;
            }

            return energy;
        }

        private void ConsumePendingImpulses()
        {
            while (pendingImpulses.Count > 0)
                ApplyImpulse(pendingImpulses.Dequeue());
        }

        private void ApplyImpulse(HexImpulseRequest request)
        {
            if (impulseProfile == null || impulseProfile.Count == 0)
                return;

            List<List<int>> targetsPerRing = new List<List<int>>(impulseProfile.Count);
            float availableWeight = 0f;

            for (int ringIndex = 0; ringIndex < impulseProfile.Count; ringIndex++)
            {
                HexImpulseRing ring = impulseProfile[ringIndex];
                List<int> targets = new List<int>();

                if (ring.ImpulseWeight > 0f)
                {
                    for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        if (fixedBoundary[cellIndex])
                            continue;

                        if (cells[cellIndex].Coordinate.DistanceTo(request.Center) == ring.Distance)
                            targets.Add(cellIndex);
                    }
                }

                targetsPerRing.Add(targets);
                if (targets.Count > 0)
                    availableWeight += ring.ImpulseWeight;
            }

            if (availableWeight <= Mathf.Epsilon)
                return;

            for (int ringIndex = 0; ringIndex < impulseProfile.Count; ringIndex++)
            {
                List<int> targets = targetsPerRing[ringIndex];
                if (targets.Count == 0)
                    continue;

                float normalizedWeight = impulseProfile[ringIndex].ImpulseWeight / availableWeight;
                float impulsePerCell = request.TotalImpulse * normalizedWeight / targets.Count;
                float velocityChange = impulsePerCell / mass;

                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    int cellIndex = targets[targetIndex];
                    velocities[cellIndex] += velocityChange;
                    sleeping[cellIndex] = false;
                    quietStepCounts[cellIndex] = 0;
                }
            }
        }

        private void SimulateSubstep(float deltaTime)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                forces[i] = fixedBoundary[i]
                    ? 0f
                    : -kOrigin * heights[i] - cOrigin * velocities[i];
            }

            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                Edge edge = edges[edgeIndex];
                int first = edge.First;
                int second = edge.Second;
                float edgeForce =
                    kNeighbor * (heights[second] - heights[first]) +
                    cNeighbor * (velocities[second] - velocities[first]);

                if (!fixedBoundary[first])
                    forces[first] += edgeForce;
                if (!fixedBoundary[second])
                    forces[second] -= edgeForce;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (fixedBoundary[i])
                {
                    nextHeights[i] = 0f;
                    nextVelocities[i] = 0f;
                    continue;
                }

                float acceleration = forces[i] / mass;
                nextVelocities[i] = velocities[i] + acceleration * deltaTime;
                nextHeights[i] = heights[i] + nextVelocities[i] * deltaTime;
            }

            UpdateSleepStates();
            Swap(ref heights, ref nextHeights);
            Swap(ref velocities, ref nextVelocities);
        }

        private void UpdateSleepStates()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (fixedBoundary[i])
                {
                    sleeping[i] = true;
                    quietStepCounts[i] = sleepSteps;
                    continue;
                }

                bool quiet =
                    Mathf.Abs(nextHeights[i]) <= sleepHeightThreshold &&
                    Mathf.Abs(nextVelocities[i]) <= sleepVelocityThreshold;

                if (quiet)
                {
                    for (int direction = 0; direction < 6; direction++)
                    {
                        int neighborIndex = neighbors[i, direction];
                        if (neighborIndex < 0)
                            continue;

                        if (Mathf.Abs(nextHeights[neighborIndex] - nextHeights[i]) > sleepHeightThreshold ||
                            Mathf.Abs(nextVelocities[neighborIndex] - nextVelocities[i]) > sleepVelocityThreshold)
                        {
                            quiet = false;
                            break;
                        }
                    }
                }

                if (!quiet)
                {
                    quietStepCounts[i] = 0;
                    sleeping[i] = false;
                    continue;
                }

                quietStepCounts[i]++;
                if (quietStepCounts[i] < sleepSteps)
                    continue;

                nextHeights[i] = 0f;
                nextVelocities[i] = 0f;
                sleeping[i] = true;
            }
        }

        private static void Swap<T>(ref T left, ref T right)
        {
            (left, right) = (right, left);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBoundaryGizmos || cells == null)
                return;

            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.75f);
            for (int i = 0; i < cells.Length && i < fixedBoundary.Length; i++)
            {
                if (fixedBoundary[i] && cells[i] != null)
                    Gizmos.DrawWireSphere(cells[i].transform.position + transform.up * 0.1f, spacing * 0.12f);
            }
        }
    }
}
