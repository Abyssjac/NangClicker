using System;
using System.Collections.Generic;
using UnityEngine;

namespace NangClicker.HexWave
{
    public readonly struct HexEnemyStackSnapshot
    {
        public int Id { get; }
        public HexCoordinate Coordinate { get; }
        public int Count { get; }
        public int TotalHealth { get; }
        public int DistanceToGoal { get; }

        public HexEnemyStackSnapshot(
            int id,
            HexCoordinate coordinate,
            int count,
            int totalHealth,
            int distanceToGoal)
        {
            Id = id;
            Coordinate = coordinate;
            Count = count;
            TotalHealth = totalHealth;
            DistanceToGoal = distanceToGoal;
        }
    }

    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class HexEnemyManager : MonoBehaviour
    {
        private sealed class EnemyStackState
        {
            public int Id;
            public HexCoordinate Coordinate;
            public int Count;
            public int TotalHealth;
        }

        private const int MaxScheduledEventsPerFixedUpdate = 128;

        [Header("References")]
        [SerializeField] private HexWaveManager waveManager;

        [Header("Enemy")]
        [Min(1)] [SerializeField] private int enemyMaxHealth = 10;
        [Min(0.01f)] [SerializeField] private float enemyStepSeconds = 3f;
        [Min(0.01f)] [SerializeField] private float enemyHurtStep = 0.1f;
        [Min(1)] [SerializeField] private int spawnCountPerStep = 2;

        [Header("Spawn Distance Field")]
        [Min(1)] [SerializeField] private int minSpawnDistance = 3;
        [Min(1)] [SerializeField] private int maxSpawnDistance = 4;

        [Header("Wave Damage")]
        [Min(0f)] [SerializeField] private float minimumDamageHeight = 0.02f;
        [Min(0f)] [SerializeField] private float heightDamageScale = 10f;

        [Header("Goal")]
        [Min(1)] [SerializeField] private int initialTargetPoints = 20;

        [Header("Random")]
        [SerializeField] private bool useFixedRandomSeed = true;
        [SerializeField] private int randomSeed = 12345;

        private readonly Dictionary<HexCoordinate, EnemyStackState> stacksByCell =
            new Dictionary<HexCoordinate, EnemyStackState>();
        private readonly Dictionary<HexCoordinate, int> distanceField =
            new Dictionary<HexCoordinate, int>();
        private readonly List<HexCoordinate> spawnCandidates = new List<HexCoordinate>();
        private readonly List<EnemyStackState> stepOrder = new List<EnemyStackState>();
        private readonly List<HexCoordinate> coordinateBuffer = new List<HexCoordinate>();
        private readonly List<HexEnemyStackSnapshot> snapshotBuffer =
            new List<HexEnemyStackSnapshot>();

        private System.Random random;
        private double simulationClock;
        private double nextHurtTime;
        private double nextEnemyStepTime;
        private int nextStackId = 1;
        private int observedTopologyVersion = -1;
        private int targetPointsRemaining;
        private int totalReachedCount;
        private bool navigationReady;
        private bool rebuildingNavigation;
        private bool catchUpWarningIssued;

        public event Action<IReadOnlyList<HexEnemyStackSnapshot>> StateChanged;
        public event Action<int> EnemiesReachedGoal;
        public event Action TargetDepleted;

        public HexWaveManager WaveManager => waveManager;
        public int StackCount => stacksByCell.Count;
        public int SpawnCandidateCount => spawnCandidates.Count;
        public int ReachableCellCount => distanceField.Count;
        public int TargetPointsRemaining => targetPointsRemaining;
        public int TotalReachedCount => totalReachedCount;
        public bool NavigationReady => navigationReady;
        public IReadOnlyList<HexEnemyStackSnapshot> CurrentSnapshot => snapshotBuffer;

        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                foreach (EnemyStackState stack in stacksByCell.Values)
                    total += stack.Count;
                return total;
            }
        }

        private void Reset()
        {
            waveManager = GetComponent<HexWaveManager>();
        }

        private void Awake()
        {
            if (waveManager == null)
                waveManager = GetComponent<HexWaveManager>();

            ResetRandom();
            ResetClock();
            targetPointsRemaining = initialTargetPoints;
        }

        private void OnEnable()
        {
            if (waveManager == null)
                waveManager = GetComponent<HexWaveManager>();

            if (waveManager != null)
                waveManager.TopologyRebuilt += OnTopologyRebuilt;
        }

        private void Start()
        {
            if (!EnsureWaveManager())
                return;

            RebuildDistanceField();
            PublishSnapshot();
        }

        private void OnDisable()
        {
            if (waveManager != null)
                waveManager.TopologyRebuilt -= OnTopologyRebuilt;
        }

        private void OnValidate()
        {
            enemyMaxHealth = Mathf.Max(1, enemyMaxHealth);
            enemyStepSeconds = Mathf.Max(0.01f, enemyStepSeconds);
            enemyHurtStep = Mathf.Max(0.01f, enemyHurtStep);
            spawnCountPerStep = Mathf.Max(1, spawnCountPerStep);
            minSpawnDistance = Mathf.Max(1, minSpawnDistance);
            maxSpawnDistance = Mathf.Max(minSpawnDistance, maxSpawnDistance);
            minimumDamageHeight = Mathf.Max(0f, minimumDamageHeight);
            heightDamageScale = Mathf.Max(0f, heightDamageScale);
            initialTargetPoints = Mathf.Max(1, initialTargetPoints);
        }

        private void FixedUpdate()
        {
            if (!EnsureWaveManager())
                return;

            if (observedTopologyVersion != waveManager.TopologyVersion)
                RebuildDistanceField();

            if (!navigationReady)
                return;

            simulationClock += Time.fixedDeltaTime;
            int processedEvents = 0;

            while (processedEvents < MaxScheduledEventsPerFixedUpdate)
            {
                bool hurtDue = simulationClock + double.Epsilon >= nextHurtTime;
                bool enemyStepDue = simulationClock + double.Epsilon >= nextEnemyStepTime;
                if (!hurtDue && !enemyStepDue)
                    break;

                if (hurtDue && (!enemyStepDue || nextHurtTime <= nextEnemyStepTime))
                {
                    ApplyWaveDamage();
                    nextHurtTime += enemyHurtStep;
                }
                else
                {
                    ExecuteEnemyStep();
                    nextEnemyStepTime += enemyStepSeconds;
                }

                processedEvents++;
            }

            if (processedEvents >= MaxScheduledEventsPerFixedUpdate && !catchUpWarningIssued)
            {
                catchUpWarningIssued = true;
                Debug.LogWarning(
                    $"[{nameof(HexEnemyManager)}] Enemy timers exceeded the per-frame catch-up limit. " +
                    "The remaining scheduled work will continue next FixedUpdate.",
                    this);
            }
        }

        public void Configure(HexWaveManager targetWaveManager)
        {
            if (waveManager != null)
                waveManager.TopologyRebuilt -= OnTopologyRebuilt;

            waveManager = targetWaveManager;

            if (isActiveAndEnabled && waveManager != null)
                waveManager.TopologyRebuilt += OnTopologyRebuilt;
        }

        public bool RebuildDistanceField()
        {
            if (rebuildingNavigation)
                return navigationReady;

            rebuildingNavigation = true;
            try
            {
                distanceField.Clear();
                spawnCandidates.Clear();
                navigationReady = false;

                if (!EnsureWaveManager())
                    return false;

                if (!waveManager.IsInitialized && !waveManager.RebuildGrid())
                    return false;

                observedTopologyVersion = waveManager.TopologyVersion;

                if (!waveManager.TryGetCellIndex(HexCoordinate.Origin, out int originIndex))
                {
                    Debug.LogError(
                        $"[{nameof(HexEnemyManager)}] The goal cell {HexCoordinate.Origin} does not exist.",
                        this);
                    return false;
                }

                if (waveManager.IsFixedBoundary(originIndex))
                {
                    Debug.LogError(
                        $"[{nameof(HexEnemyManager)}] The goal cell {HexCoordinate.Origin} is a wall and cannot be reached.",
                        this);
                    return false;
                }

                Queue<HexCoordinate> queue = new Queue<HexCoordinate>();
                distanceField.Add(HexCoordinate.Origin, 0);
                queue.Enqueue(HexCoordinate.Origin);

                while (queue.Count > 0)
                {
                    HexCoordinate current = queue.Dequeue();
                    int neighborDistance = distanceField[current] + 1;

                    for (int direction = 0; direction < 6; direction++)
                    {
                        HexCoordinate neighbor = current.GetNeighbor(direction);
                        if (distanceField.ContainsKey(neighbor))
                            continue;

                        if (!waveManager.TryGetCellIndex(neighbor, out int neighborIndex) ||
                            waveManager.IsFixedBoundary(neighborIndex))
                        {
                            continue;
                        }

                        distanceField.Add(neighbor, neighborDistance);
                        queue.Enqueue(neighbor);
                    }
                }

                for (int i = 0; i < waveManager.CellCount; i++)
                {
                    if (waveManager.IsFixedBoundary(i))
                        continue;

                    HexCellView cell = waveManager.GetCellView(i);
                    if (cell == null || cell.Coordinate == HexCoordinate.Origin)
                        continue;

                    if (!distanceField.TryGetValue(cell.Coordinate, out int distance))
                        continue;

                    if (distance >= minSpawnDistance && distance <= maxSpawnDistance)
                        spawnCandidates.Add(cell.Coordinate);
                }

                navigationReady = true;
                ValidateExistingStacks();
                return true;
            }
            finally
            {
                rebuildingNavigation = false;
            }
        }

        public bool TryGetDistance(HexCoordinate coordinate, out int distance)
        {
            return distanceField.TryGetValue(coordinate, out distance);
        }

        [ContextMenu("Spawn Configured Enemy Wave")]
        public void SpawnConfiguredWaveNow()
        {
            if (!navigationReady)
                RebuildDistanceField();

            if (SpawnEnemies(spawnCountPerStep))
                PublishSnapshot();
        }

        [ContextMenu("Advance Enemy Step")]
        public void AdvanceEnemyStepNow()
        {
            if (!navigationReady)
                RebuildDistanceField();

            ExecuteEnemyStep();
        }

        [ContextMenu("Apply Enemy Hurt Step")]
        public void ApplyHurtStepNow()
        {
            ApplyWaveDamage();
        }

        [ContextMenu("Reset Enemy Simulation")]
        public void ResetEnemySimulation()
        {
            stacksByCell.Clear();
            snapshotBuffer.Clear();
            nextStackId = 1;
            targetPointsRemaining = initialTargetPoints;
            totalReachedCount = 0;
            catchUpWarningIssued = false;
            ResetRandom();
            ResetClock();
            PublishSnapshot();
        }

        private bool EnsureWaveManager()
        {
            if (waveManager == null)
                waveManager = GetComponent<HexWaveManager>();

            if (waveManager != null)
                return true;

            enabled = false;
            Debug.LogError($"[{nameof(HexEnemyManager)}] A HexWaveManager reference is required.", this);
            return false;
        }

        private void OnTopologyRebuilt(HexWaveManager rebuiltManager)
        {
            if (rebuiltManager != waveManager || rebuildingNavigation)
                return;

            RebuildDistanceField();
            PublishSnapshot();
        }

        private void ExecuteEnemyStep()
        {
            if (!navigationReady)
                return;

            stepOrder.Clear();
            foreach (EnemyStackState stack in stacksByCell.Values)
                stepOrder.Add(stack);

            stepOrder.Sort(CompareStacksByDistance);

            for (int i = 0; i < stepOrder.Count; i++)
                MoveStackTowardGoal(stepOrder[i]);

            SpawnEnemies(spawnCountPerStep);
            PublishSnapshot();
        }

        private int CompareStacksByDistance(EnemyStackState left, EnemyStackState right)
        {
            int leftDistance = distanceField.TryGetValue(left.Coordinate, out int ld) ? ld : int.MaxValue;
            int rightDistance = distanceField.TryGetValue(right.Coordinate, out int rd) ? rd : int.MaxValue;
            int distanceComparison = leftDistance.CompareTo(rightDistance);
            return distanceComparison != 0
                ? distanceComparison
                : left.Coordinate.CompareTo(right.Coordinate);
        }

        private void MoveStackTowardGoal(EnemyStackState stack)
        {
            if (!stacksByCell.TryGetValue(stack.Coordinate, out EnemyStackState current) || current != stack)
                return;

            if (!distanceField.TryGetValue(stack.Coordinate, out int currentDistance))
            {
                Debug.LogError(
                    $"[{nameof(HexEnemyManager)}] Enemy stack {stack.Id} is on unreachable cell {stack.Coordinate}.",
                    this);
                return;
            }

            if (currentDistance == 0)
            {
                ResolveGoalArrival(stack);
                return;
            }

            coordinateBuffer.Clear();
            int requiredDistance = currentDistance - 1;
            for (int direction = 0; direction < 6; direction++)
            {
                HexCoordinate neighbor = stack.Coordinate.GetNeighbor(direction);
                if (distanceField.TryGetValue(neighbor, out int neighborDistance) &&
                    neighborDistance == requiredDistance)
                {
                    coordinateBuffer.Add(neighbor);
                }
            }

            if (coordinateBuffer.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(HexEnemyManager)}] No descending BFS neighbor exists for " +
                    $"enemy stack {stack.Id} at {stack.Coordinate}.",
                    this);
                return;
            }

            HexCoordinate previousCoordinate = stack.Coordinate;
            HexCoordinate destination = coordinateBuffer[random.Next(coordinateBuffer.Count)];
            stacksByCell.Remove(previousCoordinate);

            if (destination == HexCoordinate.Origin)
            {
                stack.Coordinate = destination;
                ResolveGoalArrival(stack);
                return;
            }

            if (stacksByCell.TryGetValue(destination, out EnemyStackState destinationStack))
            {
                destinationStack.Count += stack.Count;
                destinationStack.TotalHealth = SaturatingAdd(destinationStack.TotalHealth, stack.TotalHealth);
                return;
            }

            stack.Coordinate = destination;
            stacksByCell.Add(destination, stack);
        }

        private void ResolveGoalArrival(EnemyStackState stack)
        {
            stacksByCell.Remove(stack.Coordinate);
            int previousTargetPoints = targetPointsRemaining;
            targetPointsRemaining = Mathf.Max(0, targetPointsRemaining - stack.Count);
            totalReachedCount = SaturatingAdd(totalReachedCount, stack.Count);
            EnemiesReachedGoal?.Invoke(stack.Count);

            if (previousTargetPoints > 0 && targetPointsRemaining == 0)
                TargetDepleted?.Invoke();
        }

        private void ApplyWaveDamage()
        {
            if (stacksByCell.Count == 0 || waveManager == null)
                return;

            stepOrder.Clear();
            foreach (EnemyStackState stack in stacksByCell.Values)
                stepOrder.Add(stack);

            bool changed = false;
            for (int i = 0; i < stepOrder.Count; i++)
            {
                EnemyStackState stack = stepOrder[i];
                if (!stacksByCell.TryGetValue(stack.Coordinate, out EnemyStackState current) || current != stack)
                    continue;

                if (!waveManager.TryGetCellIndex(stack.Coordinate, out int cellIndex))
                    continue;

                float effectiveHeight = Mathf.Max(
                    0f,
                    Mathf.Abs(waveManager.GetCurrentHeight(cellIndex)) - minimumDamageHeight);
                int baseDamage = Mathf.CeilToInt(effectiveHeight * heightDamageScale);
                if (baseDamage <= 0)
                    continue;

                long scaledDamage = (long)baseDamage * stack.Count;
                stack.TotalHealth = (int)Math.Max(0L, stack.TotalHealth - scaledDamage);
                changed = true;

                if (stack.TotalHealth <= 0)
                    stacksByCell.Remove(stack.Coordinate);
            }

            if (changed)
                PublishSnapshot();
        }

        private bool SpawnEnemies(int requestedCount)
        {
            if (requestedCount <= 0)
                return false;

            if (spawnCandidates.Count == 0)
            {
                Debug.LogError(
                    $"[{nameof(HexEnemyManager)}] No valid spawn cells exist between BFS distance " +
                    $"{minSpawnDistance} and {maxSpawnDistance}.",
                    this);
                return false;
            }

            coordinateBuffer.Clear();
            for (int i = 0; i < spawnCandidates.Count; i++)
            {
                HexCoordinate coordinate = spawnCandidates[i];
                if (!stacksByCell.ContainsKey(coordinate))
                    coordinateBuffer.Add(coordinate);
            }

            Shuffle(coordinateBuffer);
            int remaining = requestedCount;
            int emptyPlacements = Mathf.Min(remaining, coordinateBuffer.Count);
            for (int i = 0; i < emptyPlacements; i++)
            {
                AddEnemy(coordinateBuffer[i]);
                remaining--;
            }

            while (remaining > 0)
            {
                int minimumCount = int.MaxValue;
                coordinateBuffer.Clear();

                for (int i = 0; i < spawnCandidates.Count; i++)
                {
                    HexCoordinate coordinate = spawnCandidates[i];
                    int count = stacksByCell.TryGetValue(coordinate, out EnemyStackState stack)
                        ? stack.Count
                        : 0;

                    if (count < minimumCount)
                    {
                        minimumCount = count;
                        coordinateBuffer.Clear();
                        coordinateBuffer.Add(coordinate);
                    }
                    else if (count == minimumCount)
                    {
                        coordinateBuffer.Add(coordinate);
                    }
                }

                HexCoordinate destination = coordinateBuffer[random.Next(coordinateBuffer.Count)];
                AddEnemy(destination);
                remaining--;
            }

            return true;
        }

        private void AddEnemy(HexCoordinate coordinate)
        {
            if (stacksByCell.TryGetValue(coordinate, out EnemyStackState stack))
            {
                stack.Count++;
                stack.TotalHealth = SaturatingAdd(stack.TotalHealth, enemyMaxHealth);
                return;
            }

            stacksByCell.Add(
                coordinate,
                new EnemyStackState
                {
                    Id = nextStackId++,
                    Coordinate = coordinate,
                    Count = 1,
                    TotalHealth = enemyMaxHealth
                });
        }

        private void ValidateExistingStacks()
        {
            if (stacksByCell.Count == 0)
                return;

            coordinateBuffer.Clear();
            foreach (KeyValuePair<HexCoordinate, EnemyStackState> pair in stacksByCell)
            {
                if (!distanceField.ContainsKey(pair.Key) ||
                    !waveManager.TryGetCellIndex(pair.Key, out int cellIndex) ||
                    waveManager.IsFixedBoundary(cellIndex))
                {
                    coordinateBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < coordinateBuffer.Count; i++)
            {
                HexCoordinate invalidCoordinate = coordinateBuffer[i];
                Debug.LogError(
                    $"[{nameof(HexEnemyManager)}] Removing an enemy stack from invalid or unreachable " +
                    $"cell {invalidCoordinate} after a topology rebuild.",
                    this);
                stacksByCell.Remove(invalidCoordinate);
            }
        }

        private void PublishSnapshot()
        {
            stepOrder.Clear();
            foreach (EnemyStackState stack in stacksByCell.Values)
                stepOrder.Add(stack);
            stepOrder.Sort(CompareStacksByDistance);

            snapshotBuffer.Clear();
            for (int i = 0; i < stepOrder.Count; i++)
            {
                EnemyStackState stack = stepOrder[i];
                int distance = distanceField.TryGetValue(stack.Coordinate, out int value) ? value : -1;
                snapshotBuffer.Add(
                    new HexEnemyStackSnapshot(
                        stack.Id,
                        stack.Coordinate,
                        stack.Count,
                        stack.TotalHealth,
                        distance));
            }

            StateChanged?.Invoke(snapshotBuffer);
        }

        private void Shuffle(List<HexCoordinate> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private void ResetRandom()
        {
            random = useFixedRandomSeed
                ? new System.Random(randomSeed)
                : new System.Random(Environment.TickCount ^ GetInstanceID());
        }

        private void ResetClock()
        {
            simulationClock = 0d;
            nextHurtTime = enemyHurtStep;
            nextEnemyStepTime = enemyStepSeconds;
        }

        private static int SaturatingAdd(int left, int right)
        {
            long result = (long)left + right;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }
}
