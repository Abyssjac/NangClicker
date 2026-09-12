using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Authoritative runtime owner of the player's current spice combination and the active customer preference.
/// A complete, order-independent match enables one runtime ScoreModifierProperty of type IncomeMultiplier.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class TasteManager : MonoBehaviour
{
    public static TasteManager Instance { get; private set; }

    [Header("Initial Availability")]
    [Tooltip("All four spices are unlocked for this first implementation. Future unlock systems can change this list at runtime.")]
    [SerializeField] private List<SpiceType> initiallyUnlockedSpices = new()
    {
        SpiceType.Sesame,
        SpiceType.Cheese,
        SpiceType.ChiliPowder,
        SpiceType.Cumin,
    };

    [Header("Score Effect")]
    [Tooltip("A non-temporary ScoreModifierProperty whose Modifier Type is IncomeMultiplier. It is active only for an exact, non-empty taste match.")]
    [SerializeField] private Key_ScoreModifierPP incomeMultiplierModifierId = Key_ScoreModifierPP.TasteIncomeMultiplier;

    private readonly List<SpiceType> unlockedSpices = new();
    private readonly List<SpiceType> currentSpices = new();
    private readonly List<SpiceType> currentPreference = new();

    private ScoreManager modifierTarget;
    private bool modifierAppliedToTarget;
    private bool appliedModifierState;

    /// <summary>Raised after the player's produced taste changes.</summary>
    public event Action<IReadOnlyList<SpiceType>> OnCurrentSpicesChanged;

    /// <summary>Raised after customer preference changes. It remains empty until RefreshPreference is called.</summary>
    public event Action<IReadOnlyList<SpiceType>> OnPreferenceChanged;

    /// <summary>Raised only when exact-match state changes.</summary>
    public event Action<bool> OnTasteMatchChanged;

    public IReadOnlyList<SpiceType> UnlockedSpices => unlockedSpices;
    public IReadOnlyList<SpiceType> CurrentSpices => currentSpices;
    public IReadOnlyList<SpiceType> CurrentPreference => currentPreference;
    public Key_ScoreModifierPP IncomeMultiplierModifierId => incomeMultiplierModifierId;
    public bool IsExactTasteMatch => IsExactMatch(currentSpices, currentPreference);
    public bool IsIncomeMultiplierActive => modifierAppliedToTarget && appliedModifierState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeUnlockedSpices();
        currentSpices.Clear();
        currentPreference.Clear();
    }

    private void Start()
    {
        SynchronizeRuntimeModifier();
    }

    private void Update()
    {
        // ScoreManager can become available after this component's Start during scene initialization.
        if (modifierTarget != ScoreManager.Instance)
            SynchronizeRuntimeModifier();
    }

    private void OnDisable()
    {
        RemoveRuntimeModifierFromPreviousTarget();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        SanitizeSpiceList(initiallyUnlockedSpices);
    }

    /// <summary>Adds one unlocked spice to the player's taste. A spice can appear at most once.</summary>
    public bool AddSpice(SpiceType spice)
    {
        if (!IsValidSpice(spice) || !unlockedSpices.Contains(spice) || currentSpices.Contains(spice))
            return false;

        bool wasMatch = IsExactTasteMatch;
        currentSpices.Add(spice);
        NotifyCurrentTasteChanged(wasMatch);
        return true;
    }

    /// <summary>Removes a single spice from the player's taste.</summary>
    public bool RemoveSpice(SpiceType spice)
    {
        int index = currentSpices.IndexOf(spice);
        if (index < 0)
            return false;

        bool wasMatch = IsExactTasteMatch;
        currentSpices.RemoveAt(index);
        NotifyCurrentTasteChanged(wasMatch);
        return true;
    }

    /// <summary>Removes all spices from the player's current taste.</summary>
    public bool ClearSpice()
    {
        if (currentSpices.Count == 0)
            return false;

        bool wasMatch = IsExactTasteMatch;
        currentSpices.Clear();
        NotifyCurrentTasteChanged(wasMatch);
        return true;
    }

    /// <summary>
    /// Creates a random non-empty combination from currently unlocked spices. This is deliberately
    /// called by the debug window for now; the later timed refresh system can call it directly.
    /// </summary>
    public bool RefreshPreference()
    {
        if (unlockedSpices.Count == 0)
            return false;

        List<SpiceType> shuffledSpices = new(unlockedSpices);
        for (int index = shuffledSpices.Count - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (shuffledSpices[index], shuffledSpices[swapIndex]) =
                (shuffledSpices[swapIndex], shuffledSpices[index]);
        }

        int selectionCount = UnityEngine.Random.Range(1, shuffledSpices.Count + 1);
        return SetPreference(shuffledSpices.GetRange(0, selectionCount));
    }

    /// <summary>
    /// Replaces the active preference. It is primarily useful for debug tools and later content
    /// systems; duplicate and invalid spice values are discarded.
    /// </summary>
    public bool SetPreference(IReadOnlyList<SpiceType> spices)
    {
        if (spices == null)
            return false;

        List<SpiceType> sanitized = new();
        for (int index = 0; index < spices.Count; index++)
        {
            SpiceType spice = spices[index];
            if (IsValidSpice(spice) && unlockedSpices.Contains(spice) && !sanitized.Contains(spice))
                sanitized.Add(spice);
        }

        if (HaveSameSpices(currentPreference, sanitized))
            return false;

        bool wasMatch = IsExactTasteMatch;
        currentPreference.Clear();
        currentPreference.AddRange(sanitized);
        OnPreferenceChanged?.Invoke(CurrentPreference);
        NotifyTasteMatchIfChanged(wasMatch);
        return true;
    }

    public bool IsSpiceUnlocked(SpiceType spice)
    {
        return IsValidSpice(spice) && unlockedSpices.Contains(spice);
    }

    private void InitializeUnlockedSpices()
    {
        unlockedSpices.Clear();
        for (int index = 0; index < initiallyUnlockedSpices.Count; index++)
        {
            SpiceType spice = initiallyUnlockedSpices[index];
            if (IsValidSpice(spice) && !unlockedSpices.Contains(spice))
                unlockedSpices.Add(spice);
        }
    }

    private void NotifyCurrentTasteChanged(bool wasMatch)
    {
        OnCurrentSpicesChanged?.Invoke(CurrentSpices);
        NotifyTasteMatchIfChanged(wasMatch);
    }

    private void NotifyTasteMatchIfChanged(bool wasMatch)
    {
        bool isMatch = IsExactTasteMatch;
        SynchronizeRuntimeModifier();

        if (wasMatch != isMatch)
            OnTasteMatchChanged?.Invoke(isMatch);
    }

    private void SynchronizeRuntimeModifier()
    {
        ScoreManager target = ScoreManager.Instance;
        if (modifierTarget != target)
        {
            RemoveRuntimeModifierFromPreviousTarget();
            modifierTarget = target;
        }

        if (modifierTarget == null)
            return;

        bool shouldBeActive = IsExactTasteMatch;
        if (modifierAppliedToTarget && appliedModifierState == shouldBeActive)
            return;

        if (!modifierTarget.SetRuntimeScoreModifierActive(incomeMultiplierModifierId, shouldBeActive))
            return;

        modifierAppliedToTarget = true;
        appliedModifierState = shouldBeActive;
    }

    private void RemoveRuntimeModifierFromPreviousTarget()
    {
        if (modifierTarget != null && modifierAppliedToTarget)
            modifierTarget.SetRuntimeScoreModifierActive(incomeMultiplierModifierId, false);

        modifierTarget = null;
        modifierAppliedToTarget = false;
        appliedModifierState = false;
    }

    private static bool IsExactMatch(IReadOnlyList<SpiceType> first, IReadOnlyList<SpiceType> second)
    {
        if (first.Count == 0 || first.Count != second.Count)
            return false;

        for (int index = 0; index < first.Count; index++)
        {
            if (!second.Contains(first[index]))
                return false;
        }

        return true;
    }

    private static bool HaveSameSpices(IReadOnlyList<SpiceType> first, IReadOnlyList<SpiceType> second)
    {
        if (first.Count != second.Count)
            return false;

        for (int index = 0; index < first.Count; index++)
        {
            if (!second.Contains(first[index]))
                return false;
        }

        return true;
    }

    private static bool IsValidSpice(SpiceType spice)
    {
        return spice != SpiceType.None;
    }

    private static void SanitizeSpiceList(List<SpiceType> spices)
    {
        if (spices == null)
            return;

        for (int index = spices.Count - 1; index >= 0; index--)
        {
            SpiceType spice = spices[index];
            if (!IsValidSpice(spice) || spices.IndexOf(spice) != index)
                spices.RemoveAt(index);
        }
    }
}
