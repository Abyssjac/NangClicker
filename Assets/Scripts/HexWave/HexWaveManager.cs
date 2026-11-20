using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NangClicker.HexWave
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class HexWaveManager : MonoBehaviour
    {
        [Header("Grid Authoring")]
        [SerializeField] private Transform cellsRoot;
        [SerializeField] private HexCellView cellPrefab;
        [Min(0.001f), SerializeField] private float spacing = 1.1f;
        [Header("Mass-Spring-Damper")]
        [Min(0.0001f), SerializeField] private float mass = 1;
        [Min(0), SerializeField] private float kOrigin = 20;
        [Min(0), SerializeField] private float cOrigin = 1.8f;
        [Min(0), SerializeField] private float kNeighbor = 35;
        [Min(0), SerializeField] private float cNeighbor = 0.6f;
        [Range(1, 16), SerializeField] private int physicsSubsteps = 1;
        [Header("Sleep")]
        [Min(0), SerializeField] private float sleepHeightThreshold = 0.0005f;
        [Min(0), SerializeField] private float sleepVelocityThreshold = 0.0005f;
        [Min(1), SerializeField] private int sleepSteps = 12;
        [Header("Debug")]
        [SerializeField] private bool drawBoundaryGizmos = true;
        private readonly Dictionary<HexCoordinate, int> coordinateToIndex = new Dictionary<HexCoordinate, int>();
        private readonly Dictionary<HexCoordinate, HexBoundarySource> sceneSources = new Dictionary<HexCoordinate, HexBoundarySource>();
        private HexCellView[] cells = Array.Empty<HexCellView>();
        private bool[] fixedBoundary = Array.Empty<bool>();
        private float[] heights = Array.Empty<float>(), previousHeights = Array.Empty<float>(), velocities = Array.Empty<float>();
        private HexWaveWorld world;
        private HexBoundaryProfileDatabase profileDatabase;
        private HexTerrainSurfaceDatabase surfaceDatabase;
        private RaycastHit[] terrainHits = new RaycastHit[32];
        private bool initialized;
        private int topologyVersion;
        public Transform CellsRoot => cellsRoot != null ? cellsRoot : transform;
        public HexCellView CellPrefab => cellPrefab;
        public float Spacing => spacing;
        public int CellCount => cells.Length;
        public bool IsInitialized => initialized;
        public int TopologyVersion => topologyVersion;
        public HexWaveWorld World => world;
        public int ActiveWaveCount => world?.Casts.Count ?? 0;
        public int PendingCastCount
        { get { int n = 0; if (world != null) foreach (var cast in world.Casts) if (cast.PendingEventCount > 0) n++; return n; } }
        public HexWaveSettings SimulationSettings => new HexWaveSettings
        {
            mass = mass, kOrigin = kOrigin, cOrigin = cOrigin, kNeighbor = kNeighbor, cNeighbor = cNeighbor,
            sleepHeight = sleepHeightThreshold, sleepVelocity = sleepVelocityThreshold,
            sleepSteps = sleepSteps, minimumSubsteps = physicsSubsteps
        };
        public event Action<HexWaveManager> TopologyRebuilt;
        private void Awake() => RebuildGrid();
        private void FixedUpdate() => AdvanceSimulation(Time.fixedDeltaTime);
        public HexBoundaryProfileProperty GetBoundaryProfile(Key_HexBoundaryProfilePP key)
        {
            if (profileDatabase == null && PropertyDatabaseManager.Instance != null)
                profileDatabase = PropertyDatabaseManager.Instance.GetDatabase<HexBoundaryProfileDatabase>();
            return profileDatabase != null ? profileDatabase.GetByEnum(key) : null;
        }
        public HexTerrainSurfaceProperty GetSurfaceProfile(Key_HexTerrainSurfacePP key)
        {
            if (surfaceDatabase == null && PropertyDatabaseManager.Instance != null)
                surfaceDatabase = PropertyDatabaseManager.Instance.GetDatabase<HexTerrainSurfaceDatabase>();
            return surfaceDatabase != null ? surfaceDatabase.GetByEnum(key) : null;
        }
        public bool TryGetCellAtWorld(Vector3 worldPosition, out HexCellView cell)
        {
            var local = CellsRoot.InverseTransformPoint(worldPosition);
            return TryGetCell(HexCoordinate.FromLocalPosition(local, spacing), out cell);
        }
        public bool TryRaycastCell(Ray ray, out HexCellView cell, out RaycastHit closest, float distance = 1000, int layerMask = ~0)
        {
            cell = null; closest = default; float best = distance;
            // Query only owned terrain hits, so sprites, the player capsule and decorative colliders never mask a tile.
            var physics = gameObject.scene.GetPhysicsScene();
            int count = physics.Raycast(ray.origin, ray.direction, terrainHits, distance, layerMask, QueryTriggerInteraction.Ignore);
            while (count == terrainHits.Length)
            {
                Array.Resize(ref terrainHits, terrainHits.Length * 2);
                count = physics.Raycast(ray.origin, ray.direction, terrainHits, distance, layerMask, QueryTriggerInteraction.Ignore);
            }
            for (int hitIndex = 0; hitIndex < count; hitIndex++)
            {
                var hit = terrainHits[hitIndex];
                if (hit.distance >= best) continue;
                var candidate = hit.collider.GetComponentInParent<HexCellView>();
                if (candidate == null || !TryGetCell(candidate.Coordinate, out var owned) || owned != candidate) continue;
                best = hit.distance; cell = candidate; closest = hit;
            }
            return cell != null;
        }
        public bool TryGetSurfacePoint(HexCoordinate coordinate, out Vector3 point)
        {
            if (TryGetCell(coordinate, out var cell)) { point = cell.SurfacePoint; return true; }
            point = default; return false;
        }
        private HexBoundarySource SceneSource(HexCoordinate coordinate)
        {
            if (!sceneSources.TryGetValue(coordinate, out var source))
            {
                var response = TryGetCell(coordinate, out var cell) && cell.IsWall ? cell.WallResponse : HexBoundarySettings.Fixed;
                var profile = GetBoundaryProfile(response.profile);
                source = new HexBoundarySource(response, true, profile != null ? profile.amplification : HexAmplifySettings.Default);
                sceneSources.Add(coordinate, source);
            }
            return source;
        }
        public HexBoundarySource GetWallSource(HexCoordinate coordinate) => world != null ? world.GetWallSource(coordinate) : SceneSource(coordinate);
        public HexSpellEdgeMode GetWallResponseMode(HexCoordinate coordinate) => GetWallSource(coordinate).Settings.mode;
        public HexMotionControl GetMotionControl(HexCoordinate coordinate) => world?.Control(coordinate) ?? HexMotionControl.Free;
        private bool BaseMovable(HexCoordinate coordinate) => coordinateToIndex.TryGetValue(coordinate, out int i) && !fixedBoundary[i];
        public bool IsMovable(HexCoordinate coordinate) => BaseMovable(coordinate) && (world == null || !world.IsBlocked(coordinate));
        public bool IsWaveWall(HexCoordinate coordinate) => !BaseMovable(coordinate) || world != null && world.IsTemporaryWall(coordinate);
        public bool CanCastSpell(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier = 1)
        { if (!initialized && !RebuildGrid()) return false; return world.CanBegin(spell, origin, direction, multiplier); }
        public HexSpellWave BeginSpell(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier = 1)
        {
            if (!initialized && !RebuildGrid()) return null;
            return world.BeginCast(spell, origin, direction, SimulationSettings, multiplier);
        }
        public bool ReleaseSpell(HexSpellWave cast)
        { bool result = world != null && world.ReleaseCast(cast); RefreshSnapshot(); return result; }
        public void CancelSpell(HexSpellWave cast) { cast?.Cancel(); RefreshSnapshot(); }
        /// <summary>Automation entry: performs the full charge, then releases. Player input uses BeginSpell/ReleaseSpell.</summary>
        public bool CastSpell(HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier = 1)
        {
            if (!initialized && !RebuildGrid()) return false;
            return world.BeginCast(spell, origin, direction, SimulationSettings, multiplier, true) != null;
        }
        public void AdvanceSimulation(float deltaTime)
        {
            if (!HexSpellProperty.Finite(deltaTime) || deltaTime <= 0 || !initialized && !RebuildGrid()) return;
            Array.Copy(heights, previousHeights, heights.Length);
            world.Step(deltaTime); RefreshSnapshot();
        }
        private void RefreshSnapshot()
        {
            if (world == null) return;
            for (int i = 0; i < cells.Length; i++)
            { heights[i] = world.Height(cells[i].Coordinate); velocities[i] = world.Velocity(cells[i].Coordinate); }
        }
        private void OnValidate()
        {
            mass = Mathf.Max(0.0001f, mass); spacing = Mathf.Max(0.001f, spacing);
            kOrigin = Mathf.Max(0, kOrigin); cOrigin = Mathf.Max(0, cOrigin);
            kNeighbor = Mathf.Max(0, kNeighbor); cNeighbor = Mathf.Max(0, cNeighbor);
            physicsSubsteps = Mathf.Clamp(physicsSubsteps, 1, 16);
            sleepHeightThreshold = Mathf.Max(0, sleepHeightThreshold); sleepVelocityThreshold = Mathf.Max(0, sleepVelocityThreshold);
            sleepSteps = Mathf.Max(1, sleepSteps);
        }
        public void ConfigureAuthoring(HexCellView prefab, Transform root, float cellSpacing)
        { cellPrefab = prefab; cellsRoot = root; spacing = Mathf.Max(0.001f, cellSpacing); }
        public bool RebuildGrid()
        {
            var discovered = CellsRoot.GetComponentsInChildren<HexCellView>(true);
            Array.Sort(discovered, (a, b) => a.Coordinate.CompareTo(b.Coordinate));
            coordinateToIndex.Clear();
            for (int i = 0; i < discovered.Length; i++)
                if (!discovered[i].Coordinate.IsValid || !coordinateToIndex.TryAdd(discovered[i].Coordinate, i))
                { Debug.LogError("[HexWaveManager] Invalid or duplicate coordinate: " + discovered[i].Coordinate, discovered[i]); initialized = false; return false; }
            cells = discovered; fixedBoundary = new bool[cells.Length];
            heights = new float[cells.Length]; previousHeights = new float[cells.Length]; velocities = new float[cells.Length];
            sceneSources.Clear();
            for (int i = 0; i < cells.Length; i++)
            {
                bool boundary = cells[i].IsWall;
                for (int d = 0; d < 6; d++) if (!coordinateToIndex.ContainsKey(cells[i].Coordinate.GetNeighbor(d))) boundary = true;
                fixedBoundary[i] = boundary;
                if (!Application.isPlaying || !initialized) cells[i].CaptureBaseLocalPosition();
                cells[i].SetRuntimeBinding(i, boundary); cells[i].ApplyHeight(0);
            }
            world = new HexWaveWorld(BaseMovable, SceneSource, GetBoundaryProfile);
            initialized = true; topologyVersion++; TopologyRebuilt?.Invoke(this); return true;
        }
        public void ResetSimulation()
        {
            world?.Reset(); sceneSources.Clear();
            Array.Clear(heights, 0, heights.Length); Array.Clear(previousHeights, 0, previousHeights.Length); Array.Clear(velocities, 0, velocities.Length);
            foreach (var cell in cells) if (cell != null) cell.ApplyHeight(0);
        }
        public bool TryGetCell(HexCoordinate coordinate, out HexCellView cell)
        {
            if (coordinateToIndex.TryGetValue(coordinate, out int i)) { cell = cells[i]; return true; }
            cell = null; return false;
        }
        public bool TryGetCellIndex(HexCoordinate coordinate, out int index) => coordinateToIndex.TryGetValue(coordinate, out index);
        public HexCellView GetCellView(int index) => index >= 0 && index < cells.Length ? cells[index] : null;
        public float GetCurrentHeight(int index) => index >= 0 && index < heights.Length ? heights[index] : 0;
        public float GetPreviousHeight(int index) => index >= 0 && index < previousHeights.Length ? previousHeights[index] : 0;
        public float GetVelocity(int index) => index >= 0 && index < velocities.Length ? velocities[index] : 0;
        // Navigation retains authored map topology; temporary spell walls only affect wave simulation.
        public bool IsFixedBoundary(int index) => index >= 0 && index < fixedBoundary.Length && fixedBoundary[index];
        public bool IsSleeping(int index) => index >= 0 && index < heights.Length &&
            Mathf.Abs(heights[index]) <= sleepHeightThreshold && Mathf.Abs(velocities[index]) <= sleepVelocityThreshold;
        public float GetTotalMechanicalEnergy()
        { float energy = 0; if (world != null) foreach (var cast in world.Casts) energy += cast.MechanicalEnergy; return energy; }
        private void OnDrawGizmosSelected()
        {
            if (!drawBoundaryGizmos) return; Gizmos.color = new Color(1, 0.25f, 0.1f, 0.75f);
            for (int i = 0; i < cells.Length; i++) if (fixedBoundary[i] && cells[i] != null)
                Gizmos.DrawWireSphere(cells[i].transform.position + transform.up * 0.1f, spacing * 0.12f);
        }
    }
}
