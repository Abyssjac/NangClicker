using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only controller for workshop upgrades and road crowd activity.
/// It reads UpgradeManager snapshots and ScoreManager.FinalAutoNangPerSec, but never mutates either system.
/// </summary>
public sealed class StoreVisualController : MonoBehaviour
{
    /// <summary>
    /// Read-only crowd state intended for the Store Visual Controller debug window.
    /// It exposes the visual system only; it never changes ScoreManager data.
    /// </summary>
    public readonly struct CrowdDebugSnapshot
    {
        public CrowdDebugSnapshot(
            double automaticNangPerSecond,
            bool usesAutomaticRateOverride,
            int tierIndex,
            double tierThreshold,
            float spawnIntervalSeconds,
            int activeCustomerCount,
            int pooledCustomerCount,
            float nextSpawnInSeconds)
        {
            AutomaticNangPerSecond = automaticNangPerSecond;
            UsesAutomaticRateOverride = usesAutomaticRateOverride;
            TierIndex = tierIndex;
            TierThreshold = tierThreshold;
            SpawnIntervalSeconds = spawnIntervalSeconds;
            ActiveCustomerCount = activeCustomerCount;
            PooledCustomerCount = pooledCustomerCount;
            NextSpawnInSeconds = nextSpawnInSeconds;
        }

        public double AutomaticNangPerSecond { get; }
        public bool UsesAutomaticRateOverride { get; }
        public int TierIndex { get; }
        public double TierThreshold { get; }
        public float SpawnIntervalSeconds { get; }
        public int ActiveCustomerCount { get; }
        public int PooledCustomerCount { get; }
        public float NextSpawnInSeconds { get; }
    }

    [Serializable]
    private struct CrowdTier
    {
        [Tooltip("This tier becomes active when Final Auto Nang / Sec reaches this value.")]
        [Min(0f)] public double minimumAutomaticNangPerSecond;

        [Tooltip("Seconds between customer-spawn attempts while this tier is active. Zero uses the built-in default for this tier (5 / 3 / 1 seconds).")]
        [Min(0f)] public float spawnIntervalSeconds;
    }

    [Header("Visual Prefabs")]
    [SerializeField] private GameObject labourPrefab;
    [SerializeField] private GameObject naanMachinePrefab;
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Transform workshopVisualRoot;
    [SerializeField] private Transform streetVisualRoot;

    [Header("Naan Machines")]
    [Tooltip("One independent machine is created at each anchor, matching the Naan Baking Machine upgrade level.")]
    [SerializeField] private Transform[] machineSpawnAnchors = new Transform[2];

    [Header("Labour Movement")]
    [SerializeField] private Transform labourLeftBoundary;
    [SerializeField] private Transform labourRightBoundary;
    [SerializeField] private float labourFixedY = -0.85f;
    [SerializeField, Min(0f)] private float labourMoveSpeed = 1.2f;
    [SerializeField, Min(0f)] private float labourMinimumTargetDistance = 0.9f;
    [SerializeField] private Vector2 labourIdleTimeRange = new(0.8f, 2.2f);

    [Header("Customer Route")]
    [SerializeField] private Transform customerLeftOffscreenPoint;
    [SerializeField] private Transform customerRightOffscreenPoint;
    [Tooltip("Opposite corners of the central shop-visit area. Customers choose a random point within it.")]
    [SerializeField] private Transform customerVisitAreaMin;
    [SerializeField] private Transform customerVisitAreaMax;
    [SerializeField, Min(0f)] private float customerMoveSpeed = 1.5f;
    [SerializeField] private Vector2 customerStayTimeRange = new(1.2f, 3f);

    [Header("Customer Crowd")]
    [SerializeField] private CrowdTier[] crowdTiers =
    {
        new CrowdTier { minimumAutomaticNangPerSecond = 5d, spawnIntervalSeconds = 5f },
        new CrowdTier { minimumAutomaticNangPerSecond = 10d, spawnIntervalSeconds = 3f },
        new CrowdTier { minimumAutomaticNangPerSecond = 20d, spawnIntervalSeconds = 1f },
    };
    [Tooltip("How often Final Auto Nang / Sec is sampled to determine the active customer tier.")]
    [SerializeField, Min(0.05f)] private float crowdRateCheckInterval = 0.5f;

    private readonly List<LabourBehaviour> activeLabours = new();
    private readonly List<GameObject> activeMachines = new();
    private readonly List<CustomerBehaviour> activeCustomers = new();
    private readonly Queue<CustomerBehaviour> inactiveCustomerPool = new();
    private readonly HashSet<string> reportedWarnings = new();

    private UpgradeManager upgradeManager;
    private ScoreManager scoreManager;
    private int currentCrowdTierIndex = int.MinValue;
    private float crowdRateCheckTimer;
    private float customerSpawnTimer = -1f;

    // Runtime-only overrides. They are deliberately not serialized, so debug experiments cannot alter gameplay.
    private bool usesDebugAutomaticNangPerSecondOverride;
    private double debugAutomaticNangPerSecond;

    /// <summary>Returns the live crowd values currently driving the road presentation.</summary>
    public CrowdDebugSnapshot GetCrowdDebugSnapshot()
    {
        double threshold = crowdTiers != null && currentCrowdTierIndex >= 0 && currentCrowdTierIndex < crowdTiers.Length
            ? crowdTiers[currentCrowdTierIndex].minimumAutomaticNangPerSecond
            : 0d;
        float spawnInterval = GetCustomerSpawnIntervalSeconds();

        return new CrowdDebugSnapshot(
            GetEffectiveAutomaticNangPerSecond(),
            usesDebugAutomaticNangPerSecondOverride,
            currentCrowdTierIndex,
            threshold,
            spawnInterval,
            activeCustomers.Count,
            inactiveCustomerPool.Count,
            customerSpawnTimer);
    }

    /// <summary>Temporarily samples a supplied automatic-production rate without modifying ScoreManager.</summary>
    public void SetDebugAutomaticNangPerSecondOverride(double automaticNangPerSecond)
    {
        if (double.IsNaN(automaticNangPerSecond) || double.IsInfinity(automaticNangPerSecond))
            return;

        usesDebugAutomaticNangPerSecondOverride = true;
        debugAutomaticNangPerSecond = Math.Max(0d, automaticNangPerSecond);
        RefreshCrowdTier(force: true);
    }

    /// <summary>Returns crowd tier selection to ScoreManager.FinalAutoNangPerSec.</summary>
    public void ClearDebugAutomaticNangPerSecondOverride()
    {
        if (!usesDebugAutomaticNangPerSecondOverride)
            return;

        usesDebugAutomaticNangPerSecondOverride = false;
        RefreshCrowdTier(force: true);
    }

    /// <summary>Removes all current customers into the existing pool without destroying visual instances.</summary>
    public void ClearCustomersForDebug()
    {
        RemoveDestroyedReferences();
        while (activeCustomers.Count > 0)
            ReturnCustomerToPool(activeCustomers[activeCustomers.Count - 1]);
    }

    /// <summary>Forces an immediate tier evaluation. A changed tier restarts its spawn countdown.</summary>
    public void ForceRefreshCrowdForDebug()
    {
        RefreshCrowdTier(force: true);
    }

    private void Awake()
    {
        workshopVisualRoot ??= transform;
        streetVisualRoot ??= transform;
    }

    private void OnEnable()
    {
        ResolveManagers();
    }

    private void Start()
    {
        ResolveManagers();
        RefreshCrowdTier(force: true);
    }

    private void OnDisable()
    {
        UnsubscribeFromUpgradeManager();
    }

    private void Update()
    {
        ResolveManagers();
        RemoveDestroyedReferences();

        crowdRateCheckTimer -= Time.deltaTime;
        if (crowdRateCheckTimer <= 0f)
        {
            crowdRateCheckTimer = Mathf.Max(0.05f, crowdRateCheckInterval);
            RefreshCrowdTier(force: false);
        }

        UpdateCustomerSpawning();
    }

    private void ResolveManagers()
    {
        UpgradeManager candidateUpgradeManager = UpgradeManager.Instance;
        if (candidateUpgradeManager != upgradeManager)
        {
            UnsubscribeFromUpgradeManager();
            upgradeManager = candidateUpgradeManager;

            if (upgradeManager != null)
            {
                upgradeManager.OnSnapshotsChanged += ApplyUpgradeSnapshots;
                ApplyUpgradeSnapshots(upgradeManager.GetSnapshots());
            }
        }

        scoreManager = ScoreManager.Instance;
    }

    private void UnsubscribeFromUpgradeManager()
    {
        if (upgradeManager != null)
            upgradeManager.OnSnapshotsChanged -= ApplyUpgradeSnapshots;

        upgradeManager = null;
    }

    private void ApplyUpgradeSnapshots(IReadOnlyList<UpgradeSlotSnapshot> snapshots)
    {
        int labourLevel = 0;
        int machineLevel = 0;

        for (int i = 0; i < snapshots.Count; i++)
        {
            UpgradeSlotSnapshot snapshot = snapshots[i];
            switch (snapshot.ProfileId)
            {
                case Key_UpgradeProfilePP.MoreWorkers:
                    labourLevel = snapshot.CurrentLevel;
                    break;
                case Key_UpgradeProfilePP.NaanBakingMachine:
                    machineLevel = snapshot.CurrentLevel;
                    break;
            }
        }

        SyncLabours(labourLevel);
        SyncMachines(machineLevel);
    }

    private void SyncLabours(int targetCount)
    {
        targetCount = Mathf.Max(0, targetCount);
        if (!TryGetLabourBounds(out float minimumX, out float maximumX))
            return;

        while (activeLabours.Count < targetCount)
        {
            if (labourPrefab == null)
            {
                ReportWarningOnce("missing-labour-prefab", $"[{nameof(StoreVisualController)}] Labour Prefab is not assigned.");
                return;
            }

            Vector3 spawnPosition = new(
                UnityEngine.Random.Range(minimumX, maximumX),
                labourFixedY,
                workshopVisualRoot.position.z);
            GameObject labourObject = Instantiate(labourPrefab, spawnPosition, Quaternion.identity, workshopVisualRoot);
            labourObject.name = $"Labour_{activeLabours.Count + 1}";

            if (!labourObject.TryGetComponent(out LabourBehaviour labour))
            {
                ReportWarningOnce("missing-labour-behaviour", $"[{nameof(StoreVisualController)}] Labour Prefab requires {nameof(LabourBehaviour)} on its root.");
                Destroy(labourObject);
                return;
            }

            labour.Initialize(
                minimumX,
                maximumX,
                labourFixedY,
                labourMoveSpeed,
                labourMinimumTargetDistance,
                labourIdleTimeRange);
            activeLabours.Add(labour);
        }

        while (activeLabours.Count > targetCount)
        {
            int lastIndex = activeLabours.Count - 1;
            LabourBehaviour labour = activeLabours[lastIndex];
            activeLabours.RemoveAt(lastIndex);
            if (labour != null)
                Destroy(labour.gameObject);
        }
    }

    private void SyncMachines(int targetCount)
    {
        targetCount = Mathf.Max(0, targetCount);
        int configuredAnchorCount = machineSpawnAnchors?.Length ?? 0;
        if (targetCount > configuredAnchorCount)
        {
            ReportWarningOnce(
                "insufficient-machine-anchors",
                $"[{nameof(StoreVisualController)}] Upgrade requests {targetCount} machines but only {configuredAnchorCount} machine anchors are configured.");
            targetCount = configuredAnchorCount;
        }

        while (activeMachines.Count < targetCount)
        {
            int anchorIndex = activeMachines.Count;
            Transform anchor = machineSpawnAnchors[anchorIndex];
            if (anchor == null)
            {
                ReportWarningOnce($"null-machine-anchor-{anchorIndex}", $"[{nameof(StoreVisualController)}] Machine anchor {anchorIndex + 1} is not assigned.");
                return;
            }

            if (naanMachinePrefab == null)
            {
                ReportWarningOnce("missing-machine-prefab", $"[{nameof(StoreVisualController)}] Naan Machine Prefab is not assigned.");
                return;
            }

            GameObject machineObject = Instantiate(naanMachinePrefab, anchor.position, anchor.rotation, workshopVisualRoot);
            machineObject.name = $"NaanMachine_{anchorIndex + 1}";
            activeMachines.Add(machineObject);
        }

        while (activeMachines.Count > targetCount)
        {
            int lastIndex = activeMachines.Count - 1;
            GameObject machine = activeMachines[lastIndex];
            activeMachines.RemoveAt(lastIndex);
            if (machine != null)
                Destroy(machine);
        }
    }

    private void RefreshCrowdTier(bool force)
    {
        int nextTierIndex = FindCrowdTier(GetEffectiveAutomaticNangPerSecond());
        if (!force && nextTierIndex == currentCrowdTierIndex)
            return;

        bool tierChanged = nextTierIndex != currentCrowdTierIndex;
        currentCrowdTierIndex = nextTierIndex;
        if (tierChanged)
            ResetCustomerSpawnTimer();
    }

    private int FindCrowdTier(double automaticNangPerSecond)
    {
        if (crowdTiers == null)
            return -1;

        int result = -1;
        double highestEligibleThreshold = double.NegativeInfinity;

        for (int i = 0; i < crowdTiers.Length; i++)
        {
            CrowdTier tier = crowdTiers[i];
            if (automaticNangPerSecond >= tier.minimumAutomaticNangPerSecond
                && tier.minimumAutomaticNangPerSecond >= highestEligibleThreshold)
            {
                result = i;
                highestEligibleThreshold = tier.minimumAutomaticNangPerSecond;
            }
        }

        return result;
    }

    private void UpdateCustomerSpawning()
    {
        if (currentCrowdTierIndex < 0 || !HasCustomerRouteConfiguration())
        {
            // Debug.LogError("No Customer Route Configuration or currentCroawnTier <0");
            return;
        }
            // return;


        // Why do u rest it here, because u reset it here Spawn Customer could never been runned, I commented here;
        // if (customerSpawnTimer < 0f)
        //     ResetCustomerSpawnTimer();

        if (customerSpawnTimer > 0f)
        {
            customerSpawnTimer -= Time.deltaTime;
            return;
        }

        if (UnityEngine.Random.value >= 0.05f)
        {
            // Debug.Log("SpawnCustomer");
            SpawnCustomer();
        }
        else
        {
            // Debug.LogError("Chance Failed");
        }
            // SpawnCustomer();

        ResetCustomerSpawnTimer();
    }

    private void ResetCustomerSpawnTimer()
    {
        customerSpawnTimer = currentCrowdTierIndex >= 0
            ? GetCustomerSpawnIntervalSeconds()
            : -1f;
    }

    private void SpawnCustomer()
    {
        if (!TryBuildCustomerRoute(out Vector3 spawnPosition, out Vector3 visitPosition, out Vector3 exitPosition))
        {
            // Debug.LogError("TryBuildCustomerROute Failed");
            return;
        }
            // return;

        CustomerBehaviour customer = TakeCustomerFromPoolOrCreate();
        if (customer == null)
        {
            // Debug.LogError("TakeCustomerFromPoolOrCreate Failed");
            return;
        }
            // return;

        customer.gameObject.SetActive(true);
        activeCustomers.Add(customer);
        customer.BeginRoute(
            spawnPosition,
            visitPosition,
            exitPosition,
            customerMoveSpeed,
            customerStayTimeRange,
            ReturnCustomerToPool);

        // Debug.Log("Customer SPawned");
    }

    private CustomerBehaviour TakeCustomerFromPoolOrCreate()
    {
        while (inactiveCustomerPool.Count > 0)
        {
            CustomerBehaviour pooledCustomer = inactiveCustomerPool.Dequeue();
            if (pooledCustomer != null)
                return pooledCustomer;
        }

        if (customerPrefab == null)
        {
            ReportWarningOnce("missing-customer-prefab", $"[{nameof(StoreVisualController)}] Customer Prefab is not assigned.");
            return null;
        }

        GameObject customerObject = Instantiate(customerPrefab, streetVisualRoot);
        customerObject.name = "Customer";
        if (customerObject.TryGetComponent(out CustomerBehaviour customer))
            return customer;

        ReportWarningOnce("missing-customer-behaviour", $"[{nameof(StoreVisualController)}] Customer Prefab requires {nameof(CustomerBehaviour)} on its root.");
        Destroy(customerObject);
        return null;
    }

    private bool TryBuildCustomerRoute(out Vector3 spawnPosition, out Vector3 visitPosition, out Vector3 exitPosition)
    {
        spawnPosition = default;
        visitPosition = default;
        exitPosition = default;

        if (!HasCustomerRouteConfiguration())
            return false;

        bool entersFromLeft = UnityEngine.Random.value < 0.5f;
        Transform entryPoint = entersFromLeft ? customerLeftOffscreenPoint : customerRightOffscreenPoint;
        Transform exitPoint = entersFromLeft ? customerRightOffscreenPoint : customerLeftOffscreenPoint;

        float minimumVisitX = Mathf.Min(customerVisitAreaMin.position.x, customerVisitAreaMax.position.x);
        float maximumVisitX = Mathf.Max(customerVisitAreaMin.position.x, customerVisitAreaMax.position.x);
        float minimumVisitY = Mathf.Min(customerVisitAreaMin.position.y, customerVisitAreaMax.position.y);
        float maximumVisitY = Mathf.Max(customerVisitAreaMin.position.y, customerVisitAreaMax.position.y);
        float y = UnityEngine.Random.Range(minimumVisitY, maximumVisitY);

        spawnPosition = new Vector3(entryPoint.position.x, y, entryPoint.position.z);
        visitPosition = new Vector3(UnityEngine.Random.Range(minimumVisitX, maximumVisitX), y, entryPoint.position.z);
        exitPosition = new Vector3(exitPoint.position.x, y, exitPoint.position.z);
        return true;
    }

    private void ReturnCustomerToPool(CustomerBehaviour customer)
    {
        if (customer == null)
            return;

        activeCustomers.Remove(customer);
        customer.gameObject.SetActive(false);
        inactiveCustomerPool.Enqueue(customer);
    }

    private bool TryGetLabourBounds(out float minimumX, out float maximumX)
    {
        minimumX = 0f;
        maximumX = 0f;
        if (labourLeftBoundary == null || labourRightBoundary == null)
        {
            ReportWarningOnce("missing-labour-boundaries", $"[{nameof(StoreVisualController)}] Both labour boundaries must be assigned.");
            return false;
        }

        minimumX = Mathf.Min(labourLeftBoundary.position.x, labourRightBoundary.position.x);
        maximumX = Mathf.Max(labourLeftBoundary.position.x, labourRightBoundary.position.x);
        return true;
    }

    private bool HasCustomerRouteConfiguration()
    {
        bool isConfigured = customerLeftOffscreenPoint != null
                            && customerRightOffscreenPoint != null
                            && customerVisitAreaMin != null
                            && customerVisitAreaMax != null;
        if (!isConfigured)
            ReportWarningOnce("missing-customer-route", $"[{nameof(StoreVisualController)}] Customer route transforms are not fully assigned.");

        return isConfigured;
    }

    private void RemoveDestroyedReferences()
    {
        activeLabours.RemoveAll(labour => labour == null);
        activeMachines.RemoveAll(machine => machine == null);
        activeCustomers.RemoveAll(customer => customer == null);
    }

    private void ReportWarningOnce(string key, string message)
    {
        if (reportedWarnings.Add(key))
            Debug.LogWarning(message, this);
    }

    private float GetCustomerSpawnIntervalSeconds()
    {
        if (crowdTiers == null || currentCrowdTierIndex < 0 || currentCrowdTierIndex >= crowdTiers.Length)
            return 0f;

        float configuredInterval = crowdTiers[currentCrowdTierIndex].spawnIntervalSeconds;
        if (configuredInterval > 0f)
            return configuredInterval;

        // Existing scene instances predate spawnIntervalSeconds. Preserve the agreed three-tier behaviour without
        // requiring their serialized data to be manually migrated before Play Mode.
        switch (currentCrowdTierIndex)
        {
            case 0:
                return 5f;
            case 1:
                return 3f;
            default:
                return 1f;
        }
    }

    private double GetEffectiveAutomaticNangPerSecond()
    {
        if (usesDebugAutomaticNangPerSecondOverride)
            return debugAutomaticNangPerSecond;

        return scoreManager != null ? scoreManager.FinalAutoNangPerSec : 0d;
    }
}
