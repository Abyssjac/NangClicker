using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

/// <summary>
/// The authoritative runtime state for all upgrades. It owns unlock state and levels;
/// UI receives snapshots and never decides whether a purchase is valid.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    private UpgradeProfileDatabase upgradeProfileDatabase;
    private readonly List<UpgradeProfileProperty> orderedProfiles = new();
    private readonly Dictionary<Key_UpgradeProfilePP, UpgradeProfileProperty> profilesById = new();
    private readonly HashSet<Key_UpgradeProfilePP> unlockedProfileIds = new();
    private readonly Dictionary<Key_UpgradeProfilePP, int> purchasedLevels = new();
    private readonly HashSet<string> reportedProblems = new();

    private bool isInitialized;
    private int nextUnlockIndex;

    /// <summary>
    /// Raised only when unlocked content or a purchased level has changed.
    /// The payload is the complete set of currently unlocked upgrade snapshots.
    /// </summary>
    public event Action<IReadOnlyList<UpgradeSlotSnapshot>> OnSnapshotsChanged;

    public UpgradeProfileDatabase UpgradeProfileDatabase => upgradeProfileDatabase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!EnsureInitialized())
            return;

        if (EvaluateUnlocks())
            PublishSnapshots();
    }

    /// <summary>
    /// Returns a new complete display snapshot. It contains unlocked profiles only.
    /// </summary>
    public IReadOnlyList<UpgradeSlotSnapshot> GetSnapshots()
    {
        return EnsureInitialized() ? BuildSnapshots() : Array.Empty<UpgradeSlotSnapshot>();
    }

    /// <summary>
    /// Attempts exactly one next level of the requested profile. The target modifier and price
    /// are resolved from the authoritative Profile and ScoreModifier databases.
    /// </summary>
    public bool TryPurchase(Key_UpgradeProfilePP profileId)
    {
        if (!EnsureInitialized()
            || !profilesById.TryGetValue(profileId, out UpgradeProfileProperty profile)
            || !unlockedProfileIds.Contains(profileId))
        {
            return false;
        }

        int currentLevel = GetPurchasedLevel(profileId);
        if (currentLevel >= profile.LevelModifiers.Count)
            return false;

        Key_ScoreModifierPP modifierId = profile.LevelModifiers[currentLevel];
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager == null
            || modifierId == Key_ScoreModifierPP.None
            || !scoreManager.TryGetScoreModifierProperty(modifierId, out ScoreModifierProperty modifier)
            || !IsPurchasablePermanentModifier(modifier))
        {
            ReportProblemOnce(
                $"purchase-{profileId}-{currentLevel}",
                $"[{nameof(UpgradeManager)}] Profile '{profileId}' level {currentLevel} does not reference a valid permanent ScoreModifierProperty.");
            return false;
        }

        double price = modifier.UpgradePrice;
        if (!scoreManager.TrySpendMoney(price))
            return false;

        // TweakScore remains the sole path that mutates the actual score formula.
        if (!scoreManager.TweakScore(modifierId))
        {
            // The property was validated above. A refund still protects money if a future ScoreManager rule rejects it.
            scoreManager.AddMoney(price);
            ReportProblemOnce(
                $"apply-{profileId}-{currentLevel}",
                $"[{nameof(UpgradeManager)}] ScoreManager rejected purchased modifier '{modifierId}'; money was refunded.");
            return false;
        }

        purchasedLevels[profileId] = currentLevel + 1;
        PublishSnapshots();
        return true;
    }

    private bool EnsureInitialized()
    {
        if (isInitialized)
            return true;

        if (PropertyDatabaseManager.Instance == null || ScoreManager.Instance == null)
            return false;

        upgradeProfileDatabase ??= PropertyDatabaseManager.Instance.GetDatabase<UpgradeProfileDatabase>();
        if (upgradeProfileDatabase == null)
            return false;

        HashSet<UpgradeSlotId> usedSlots = new();
        foreach (UpgradeProfileProperty profile in upgradeProfileDatabase.Entries)
        {
            if (!IsProfileConfigurationValid(profile, usedSlots))
                continue;

            profilesById.Add(profile.EnumKey, profile);
            orderedProfiles.Add(profile);
        }

        orderedProfiles.Sort(CompareProfilesByUnlockOrder);
        isInitialized = true;

        // This handles both a fresh game and a manager that appears after the score has already grown.
        if (EvaluateUnlocks())
            PublishSnapshots();

        return true;
    }

    private bool IsProfileConfigurationValid(UpgradeProfileProperty profile, HashSet<UpgradeSlotId> usedSlots)
    {
        if (profile == null)
        {
            ReportProblemOnce("null-profile", $"[{nameof(UpgradeManager)}] The UpgradeProfileDatabase contains a null entry.");
            return false;
        }

        if (profile.EnumKey == Key_UpgradeProfilePP.None || profilesById.ContainsKey(profile.EnumKey))
        {
            ReportProblemOnce(
                $"profile-id-{profile.name}",
                $"[{nameof(UpgradeManager)}] Upgrade Profile '{profile.name}' has an empty or duplicate ProfileId.");
            return false;
        }

        if (profile.SlotId == UpgradeSlotId.None || !usedSlots.Add(profile.SlotId))
        {
            ReportProblemOnce(
                $"slot-id-{profile.name}",
                $"[{nameof(UpgradeManager)}] Upgrade Profile '{profile.name}' has an empty or duplicate SlotId.");
            return false;
        }

        if (!IsFinite(profile.RequiredPeakMoney) || profile.RequiredPeakMoney < 0d)
        {
            ReportProblemOnce(
                $"threshold-{profile.name}",
                $"[{nameof(UpgradeManager)}] Upgrade Profile '{profile.name}' has an invalid Required Peak Money value.");
            return false;
        }

        if (profile.LevelModifiers == null || profile.LevelModifiers.Count == 0)
        {
            ReportProblemOnce(
                $"levels-{profile.name}",
                $"[{nameof(UpgradeManager)}] Upgrade Profile '{profile.name}' has no level modifiers.");
            return false;
        }

        return true;
    }

    private bool EvaluateUnlocks()
    {
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
            return false;

        bool unlockedAny = false;
        double highestMoneyReached = scoreManager.HighestMoneyReached;

        // Ordered thresholds make this a single forward pass. `while` deliberately unlocks every crossed threshold.
        while (nextUnlockIndex < orderedProfiles.Count
               && highestMoneyReached >= orderedProfiles[nextUnlockIndex].RequiredPeakMoney)
        {
            UpgradeProfileProperty profile = orderedProfiles[nextUnlockIndex++];
            unlockedAny |= unlockedProfileIds.Add(profile.EnumKey);
        }

        return unlockedAny;
    }

    private List<UpgradeSlotSnapshot> BuildSnapshots()
    {
        List<UpgradeSlotSnapshot> snapshots = new(unlockedProfileIds.Count);
        for (int i = 0; i < orderedProfiles.Count; i++)
        {
            UpgradeProfileProperty profile = orderedProfiles[i];
            if (!unlockedProfileIds.Contains(profile.EnumKey))
                continue;

            if (TryBuildSnapshot(profile, out UpgradeSlotSnapshot snapshot))
                snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private bool TryBuildSnapshot(UpgradeProfileProperty profile, out UpgradeSlotSnapshot snapshot)
    {
        snapshot = default;

        int currentLevel = GetPurchasedLevel(profile.EnumKey);
        int maxLevel = profile.LevelModifiers.Count;
        bool isCompleted = currentLevel >= maxLevel;
        int displayLevel = isCompleted ? maxLevel - 1 : currentLevel;

        if (displayLevel < 0 || displayLevel >= maxLevel)
            return false;

        Key_ScoreModifierPP displayModifierId = profile.LevelModifiers[displayLevel];
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager == null
            || !scoreManager.TryGetScoreModifierProperty(displayModifierId, out ScoreModifierProperty displayModifier)
            || displayModifier == null)
        {
            ReportProblemOnce(
                $"snapshot-{profile.EnumKey}-{displayLevel}",
                $"[{nameof(UpgradeManager)}] Upgrade Profile '{profile.EnumKey}' cannot resolve its display ScoreModifierProperty.");
            return false;
        }

        snapshot = new UpgradeSlotSnapshot(
            profile.SlotId,
            profile.EnumKey,
            displayModifier.Icon,
            displayModifier.DisplayName,
            displayModifier.Description,
            isCompleted ? 0d : displayModifier.UpgradePrice,
            currentLevel,
            maxLevel,
            isCompleted);
        return true;
    }

    private int GetPurchasedLevel(Key_UpgradeProfilePP profileId)
    {
        return purchasedLevels.TryGetValue(profileId, out int level) ? level : 0;
    }

    private void PublishSnapshots()
    {
        OnSnapshotsChanged?.Invoke(BuildSnapshots());
    }

    private static int CompareProfilesByUnlockOrder(UpgradeProfileProperty left, UpgradeProfileProperty right)
    {
        int byThreshold = left.RequiredPeakMoney.CompareTo(right.RequiredPeakMoney);
        return byThreshold != 0
            ? byThreshold
            : Comparer<Key_UpgradeProfilePP>.Default.Compare(left.EnumKey, right.EnumKey);
    }

    private static bool IsPurchasablePermanentModifier(ScoreModifierProperty modifier)
    {
        return modifier != null
               && !modifier.IsTemporary
               && modifier.ModifierType != ScoreModifierType.None
               && IsFinite(modifier.Amount)
               && IsFinite(modifier.UpgradePrice)
               && modifier.UpgradePrice >= 0d
               && (!IsMultiplier(modifier.ModifierType) || modifier.Amount > 0d);
    }

    private static bool IsMultiplier(ScoreModifierType modifierType)
    {
        return modifierType == ScoreModifierType.UnitSalePriceMultiplier
               || modifierType == ScoreModifierType.IncomeMultiplier;
    }

    private void ReportProblemOnce(string key, string message)
    {
        if (reportedProblems.Add(key))
            Debug.LogError(message, this);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
