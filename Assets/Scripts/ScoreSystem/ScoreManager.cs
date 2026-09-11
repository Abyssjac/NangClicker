using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private const float MinimumProductionTickInterval = 0.01f;

    [Serializable]
    private struct TemporaryScoreModifierState
    {
        public float RemainingTime;
    }

    public static ScoreManager Instance { get; private set; }

    // The database is resolved from PropertyDatabaseManager and cached at runtime.
    // It intentionally is not a scene-level serialized reference.
    private ScoreModifierDatabase scoreModifierDatabase;

    [Header("Authoritative Base State")]
    [SerializeField, Min(0f)] private double baseUnitSalePrice = 1d;
    [SerializeField, Min(0f)] private double nangPerSecond = 1d;
    [SerializeField, Min(0f)] private double money;
    [SerializeField, Min(0f)] private double nangAmt;

    [Header("Production Tick")]
    [Tooltip("Production and sales are settled in fixed game-time intervals.")]
    [SerializeField, Min(MinimumProductionTickInterval)] private float productionTickInterval = 1f;
    [Tooltip("The Nang amount queued by one manual click.")]
    [SerializeField, Min(0f)] private double manualNangPerClick = 1d;

    [Header("Debug")]
    [SerializeField] private bool isSimulationPaused;

    private readonly List<Key_ScoreModifierPP> permanentModifierHistory = new();
    private readonly Dictionary<Key_ScoreModifierPP, TemporaryScoreModifierState> temporaryModifiers = new();
    private readonly List<Key_ScoreModifierPP> temporaryKeyBuffer = new();

    private bool isInitialized;
    private float productionTickTimer;
    private double pendingManualNang;
    private double lastTickAutomaticNang;
    private double lastTickManualNang;
    private double lastTickTotalNang;
    private double lastTickIncome;

    public event Action OnScoreChanged;
    public event Action OnModifiersChanged;
    public event Action OnProductionTick;

    public ScoreModifierDatabase ScoreModifierDatabase => scoreModifierDatabase;
    public double BaseUnitSalePrice => baseUnitSalePrice;
    public double NangPerSecond => nangPerSecond;
    public double Money => money;
    public double NangAmt => nangAmt;
    public bool IsSimulationPaused => isSimulationPaused;
    public float ProductionTickInterval => Mathf.Max(MinimumProductionTickInterval, productionTickInterval);
    public float ProductionTickElapsed => productionTickTimer;
    public float TimeUntilNextProductionTick => Mathf.Max(0f, ProductionTickInterval - productionTickTimer);
    public double ManualNangPerClick => manualNangPerClick;
    public double PendingManualNang => pendingManualNang;
    public double LastTickAutomaticNang => lastTickAutomaticNang;
    public double LastTickManualNang => lastTickManualNang;
    public double LastTickTotalNang => lastTickTotalNang;
    public double LastTickIncome => lastTickIncome;

    public double UnitSalePriceAdditiveRate { get; private set; }
    public double UnitSalePriceMultiplier { get; private set; } = 1d;
    public double UnitSalePriceFlatBonus { get; private set; }
    public double IncomeAdditiveRate { get; private set; }
    public double IncomeMultiplier { get; private set; } = 1d;
    public double UnitSalePrice { get; private set; }
    public double FinalSaleValuePerNang { get; private set; }
    public double IncomePerSecond { get; private set; }

    public IReadOnlyList<Key_ScoreModifierPP> PermanentModifierHistory => permanentModifierHistory;
    public IReadOnlyDictionary<Key_ScoreModifierPP, float> TemporaryModifierRemainingTimes
    {
        get
        {
            Dictionary<Key_ScoreModifierPP, float> result = new();
            foreach (KeyValuePair<Key_ScoreModifierPP, TemporaryScoreModifierState> pair in temporaryModifiers)
                result.Add(pair.Key, pair.Value.RemainingTime);
            return result;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        isInitialized = ResolveDatabase();
        RecalculateScores();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            isInitialized = ResolveDatabase();
            if (!isInitialized)
                return;

            RecalculateScores();
        }

        if (isSimulationPaused)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        // A tick uses the final price at the moment it is settled, so modifiers advance first.
        AdvanceTemporaryModifiers(deltaTime);
        AdvanceProductionTicks(deltaTime);
    }

    /// <summary>
    /// Applies a score Property by its enum identity. A Property with DefaultDuration <= 0 is permanent.
    /// A temporary Property uses its DefaultDuration unless a positive duration override is supplied.
    /// </summary>
    public bool TweakScore(Key_ScoreModifierPP propertyId, float durationOverride = -1f)
    {
        if (!TryGetScoreModifierProperty(propertyId, out ScoreModifierProperty property))
        {
            Debug.LogError($"[{nameof(ScoreManager)}] No {nameof(ScoreModifierProperty)} found for id '{propertyId}'.", this);
            return false;
        }

        if (property.IsTemporary)
        {
            float duration = durationOverride >= 0f ? durationOverride : property.DefaultDuration;
            if (duration <= 0f)
            {
                Debug.LogError($"[{nameof(ScoreManager)}] Temporary Property '{property.DisplayName}' requires a positive duration.", this);
                return false;
            }

            return TweakScore(property.ModifierType, property.Amount, propertyId, duration);
        }

        if (durationOverride >= 0f)
        {
            Debug.LogError($"[{nameof(ScoreManager)}] Permanent Property '{property.DisplayName}' cannot receive a duration override.", this);
            return false;
        }

        return TweakScore(property.ModifierType, property.Amount, propertyId, 0f);
    }

    public bool TryGetScoreModifierProperty(Key_ScoreModifierPP propertyId, out ScoreModifierProperty property)
    {
        property = null;
        return ResolveDatabase() && scoreModifierDatabase.TryGetByEnum(propertyId, out property);
    }

    public ScoreModifierProperty GetScoreModifierProperty(Key_ScoreModifierPP propertyId)
    {
        return TryGetScoreModifierProperty(propertyId, out ScoreModifierProperty property) ? property : null;
    }

    public void SetSimulationPaused(bool paused)
    {
        isSimulationPaused = paused;
    }

    public void AddMoney(double amount)
    {
        if (!IsFinite(amount))
        {
            Debug.LogError($"[{nameof(ScoreManager)}] Cannot add a non-finite money value.", this);
            return;
        }

        money = Math.Max(0d, money + amount);
        OnScoreChanged?.Invoke();
    }

    public bool TrySpendMoney(double amount)
    {
        if (!IsFinite(amount) || amount < 0d || money < amount)
            return false;

        money -= amount;
        OnScoreChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Queues one manual click using the currently configured per-click Nang amount.
    /// The queued Nang is committed at the next production tick.
    /// </summary>
    public bool QueueManualNangClick()
    {
        return QueueManualNangProduction(manualNangPerClick);
    }

    /// <summary>
    /// Queues manual Nang production for the next production tick. Gameplay should send the
    /// amount actually produced after its own click/resource validation has succeeded.
    /// </summary>
    public bool QueueManualNangProduction(double amount)
    {
        if (!IsFinite(amount) || amount <= 0d)
            return false;

        pendingManualNang += amount;
        OnScoreChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Kept for existing callers. Production is now queued and settled centrally on the next tick.
    /// </summary>
    public void RecordNangProduced(double amount)
    {
        QueueManualNangProduction(amount);
    }

    public void SetBaseUnitSalePrice(double value)
    {
        if (!IsFinite(value) || value < 0d)
            return;

        baseUnitSalePrice = value;
        RecalculateScores();
        OnScoreChanged?.Invoke();
    }

    public void SetNangPerSecond(double value)
    {
        if (!IsFinite(value) || value < 0d)
            return;

        nangPerSecond = value;
        RecalculateScores();
        OnScoreChanged?.Invoke();
    }

    // The low-level modifier entry point intentionally stays private. Gameplay applies Property ids only.
    private bool TweakScore(
        ScoreModifierType modifierType,
        double amount,
        Key_ScoreModifierPP sourceId,
        float duration)
    {
        if (!CanApplyModifier(modifierType, amount))
            return false;

        if (duration > 0f)
        {
            if (temporaryModifiers.TryGetValue(sourceId, out TemporaryScoreModifierState activeState))
            {
                // The same temporary Property extends time only; its formula contribution remains one stack.
                activeState.RemainingTime += duration;
                temporaryModifiers[sourceId] = activeState;
            }
            else
            {
                temporaryModifiers.Add(sourceId, new TemporaryScoreModifierState { RemainingTime = duration });
            }
        }
        else
        {
            // Permanent Properties deliberately retain duplicate entries: every application is a score stack and progress record.
            permanentModifierHistory.Add(sourceId);
        }

        RecalculateScores();
        OnModifiersChanged?.Invoke();
        OnScoreChanged?.Invoke();
        return true;
    }

    private void AdvanceTemporaryModifiers(float deltaTime)
    {
        if (temporaryModifiers.Count == 0)
            return;

        temporaryKeyBuffer.Clear();
        foreach (KeyValuePair<Key_ScoreModifierPP, TemporaryScoreModifierState> pair in temporaryModifiers)
            temporaryKeyBuffer.Add(pair.Key);

        bool expiredAnyModifier = false;
        for (int i = 0; i < temporaryKeyBuffer.Count; i++)
        {
            Key_ScoreModifierPP propertyId = temporaryKeyBuffer[i];
            TemporaryScoreModifierState state = temporaryModifiers[propertyId];
            state.RemainingTime -= deltaTime;

            if (state.RemainingTime <= 0f)
            {
                temporaryModifiers.Remove(propertyId);
                expiredAnyModifier = true;
            }
            else
            {
                temporaryModifiers[propertyId] = state;
            }
        }

        if (!expiredAnyModifier)
            return;

        RecalculateScores();
        OnModifiersChanged?.Invoke();
        OnScoreChanged?.Invoke();
    }

    private void AdvanceProductionTicks(float deltaTime)
    {
        productionTickTimer += deltaTime;
        float interval = ProductionTickInterval;

        // Preserve the remainder and catch up all full ticks after a slow frame.
        while (productionTickTimer >= interval)
        {
            productionTickTimer -= interval;
            SettleProductionTick(interval);
        }
    }

    private void SettleProductionTick(float tickDuration)
    {
        lastTickAutomaticNang = nangPerSecond * tickDuration;
        lastTickManualNang = pendingManualNang;
        lastTickTotalNang = lastTickAutomaticNang + lastTickManualNang;
        lastTickIncome = FinalSaleValuePerNang * lastTickTotalNang;
        pendingManualNang = 0d;

        if (lastTickTotalNang != 0d)
        {
            nangAmt += lastTickTotalNang;
            money += lastTickIncome;
            OnScoreChanged?.Invoke();
        }

        OnProductionTick?.Invoke();
    }

    private void RecalculateScores()
    {
        UnitSalePriceAdditiveRate = 0d;
        UnitSalePriceMultiplier = 1d;
        UnitSalePriceFlatBonus = 0d;
        IncomeAdditiveRate = 0d;
        IncomeMultiplier = 1d;

        if (ResolveDatabase())
        {
            for (int i = 0; i < permanentModifierHistory.Count; i++)
                ApplyPropertyContribution(permanentModifierHistory[i]);

            foreach (KeyValuePair<Key_ScoreModifierPP, TemporaryScoreModifierState> pair in temporaryModifiers)
            {
                if (pair.Value.RemainingTime > 0f)
                    ApplyPropertyContribution(pair.Key);
            }
        }

        UnitSalePrice = (((1d + UnitSalePriceAdditiveRate) * UnitSalePriceMultiplier * baseUnitSalePrice)
                         + UnitSalePriceFlatBonus);
        FinalSaleValuePerNang = UnitSalePrice * (1d + IncomeAdditiveRate) * IncomeMultiplier;
        IncomePerSecond = FinalSaleValuePerNang * nangPerSecond;
    }

    private void ApplyPropertyContribution(Key_ScoreModifierPP propertyId)
    {
        if (!scoreModifierDatabase.TryGetByEnum(propertyId, out ScoreModifierProperty property) || property == null)
            return;

        switch (property.ModifierType)
        {
            case ScoreModifierType.UnitSalePriceAdditiveRate:
                UnitSalePriceAdditiveRate += property.Amount;
                break;
            case ScoreModifierType.UnitSalePriceMultiplier:
                UnitSalePriceMultiplier *= property.Amount;
                break;
            case ScoreModifierType.UnitSalePriceFlatBonus:
                UnitSalePriceFlatBonus += property.Amount;
                break;
            case ScoreModifierType.IncomeAdditiveRate:
                IncomeAdditiveRate += property.Amount;
                break;
            case ScoreModifierType.IncomeMultiplier:
                IncomeMultiplier *= property.Amount;
                break;
        }
    }

    private bool ResolveDatabase()
    {
        if (scoreModifierDatabase != null)
            return true;

        if (PropertyDatabaseManager.Instance == null)
            return false;

        scoreModifierDatabase = PropertyDatabaseManager.Instance.GetDatabase<ScoreModifierDatabase>();
        return scoreModifierDatabase != null;
    }

    private bool CanApplyModifier(ScoreModifierType modifierType, double amount)
    {
        if (modifierType == ScoreModifierType.None || !IsFinite(amount))
        {
            Debug.LogError($"[{nameof(ScoreManager)}] Invalid modifier type or amount.", this);
            return false;
        }

        bool isMultiplier = modifierType == ScoreModifierType.UnitSalePriceMultiplier
                            || modifierType == ScoreModifierType.IncomeMultiplier;
        if (isMultiplier && amount <= 0d)
        {
            Debug.LogError($"[{nameof(ScoreManager)}] Multiplier modifiers must be greater than zero.", this);
            return false;
        }

        return true;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
