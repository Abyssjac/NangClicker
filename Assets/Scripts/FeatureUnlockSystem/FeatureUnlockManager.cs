using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unlocks whole gameplay features from ScoreManager's cumulative Nang production.
/// Unlike UpgradeManager, these milestones have no price, level, or upgrade-slot representation.
/// </summary>
[DisallowMultipleComponent]
public sealed class FeatureUnlockManager : MonoBehaviour
{
    [Serializable]
    private struct FeatureUnlockEntry
    {
        public FeatureUnlockId featureId;
        [Min(0f)] public double requiredTotalNang;
        [Tooltip("A component implementing IFeatureUnlockable, such as FurnaceHeatManager or TasteManager.")]
        public MonoBehaviour unlockTarget;
        [Tooltip("Optional 3D/UI/interactable root hidden before this feature is unlocked. Do not place the manager under this root.")]
        public GameObject visualRoot;
    }

    [SerializeField] private FeatureUnlockEntry[] featureUnlocks =
    {
        new FeatureUnlockEntry { featureId = FeatureUnlockId.Furnace, requiredTotalNang = 500d },
        new FeatureUnlockEntry { featureId = FeatureUnlockId.Taste, requiredTotalNang = 1000d },
    };

    private readonly HashSet<FeatureUnlockId> unlockedFeatures = new();
    private readonly HashSet<string> reportedProblems = new();
    private ScoreManager scoreManager;

    public event Action<FeatureUnlockId> OnFeatureUnlocked;

    private void OnEnable()
    {
        ResolveScoreManager();
    }

    private void Start()
    {
        ResolveScoreManager();
        ApplyInitialLockedState();
        EvaluateUnlocks();
    }

    private void Update()
    {
        ResolveScoreManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromScoreManager();
    }

    public bool IsFeatureUnlocked(FeatureUnlockId featureId)
    {
        return unlockedFeatures.Contains(featureId);
    }

    private void ResolveScoreManager()
    {
        ScoreManager candidate = ScoreManager.Instance;
        if (candidate == scoreManager)
            return;

        UnsubscribeFromScoreManager();
        scoreManager = candidate;
        if (scoreManager == null)
            return;

        scoreManager.OnScoreChanged += EvaluateUnlocks;
        EvaluateUnlocks();
    }

    private void UnsubscribeFromScoreManager()
    {
        if (scoreManager != null)
            scoreManager.OnScoreChanged -= EvaluateUnlocks;

        scoreManager = null;
    }

    private void ApplyInitialLockedState()
    {
        for (int index = 0; index < featureUnlocks.Length; index++)
        {
            FeatureUnlockEntry entry = featureUnlocks[index];
            if (entry.featureId == FeatureUnlockId.None || unlockedFeatures.Contains(entry.featureId))
                continue;

            if (entry.unlockTarget is IFeatureUnlockable unlockable)
                unlockable.SetUnlocked(false);

            if (entry.visualRoot != null)
                entry.visualRoot.SetActive(false);
        }
    }

    private void EvaluateUnlocks()
    {
        if (scoreManager == null)
            return;

        double totalNangProduced = scoreManager.NangAmt;
        for (int index = 0; index < featureUnlocks.Length; index++)
        {
            FeatureUnlockEntry entry = featureUnlocks[index];
            if (entry.featureId == FeatureUnlockId.None
                || unlockedFeatures.Contains(entry.featureId)
                || totalNangProduced < entry.requiredTotalNang)
            {
                continue;
            }

            if (entry.unlockTarget is not IFeatureUnlockable unlockable)
            {
                ReportProblemOnce(
                    $"target-{entry.featureId}",
                    $"[{nameof(FeatureUnlockManager)}] Feature '{entry.featureId}' needs a MonoBehaviour implementing {nameof(IFeatureUnlockable)}.");
                continue;
            }

            unlockable.SetUnlocked(true);
            if (entry.visualRoot != null)
                entry.visualRoot.SetActive(true);

            unlockedFeatures.Add(entry.featureId);
            OnFeatureUnlocked?.Invoke(entry.featureId);
        }
    }

    private void ReportProblemOnce(string key, string message)
    {
        if (reportedProblems.Add(key))
            Debug.LogError(message, this);
    }
}
