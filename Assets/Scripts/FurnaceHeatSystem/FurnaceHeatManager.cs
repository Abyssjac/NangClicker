using System;
using UnityEngine;

/// <summary>
/// Authoritative runtime state for the furnace heat mini-game. It owns only heat state and
/// conditionally asks ScoreManager to activate its configured runtime IncomeMultiplier Property.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class FurnaceHeatManager : MonoBehaviour
{
    private const float MinimumSecondsPerPoint = 0.001f;

    public static FurnaceHeatManager Instance { get; private set; }

    [Header("Availability")]
    [SerializeField] private bool startsUnlocked = true;

    [Header("Heat Range")]
    [SerializeField, Range(0f, 100f)] private float initialHeat;
    [SerializeField, Range(0f, 100f)] private float profitableHeatMin = 90f;
    [SerializeField, Range(0f, 100f)] private float profitableHeatMax = 100f;

    [Header("Movement")]
    [Tooltip("Natural cooling duration for one heat point. A value of 3 means the pointer loses one point every three seconds.")]
    [SerializeField, Min(MinimumSecondsPerPoint)] private float secondsPerHeatPoint = 3f;
    [Tooltip("Pointer travel speed while the player is manually setting the heat.")]
    [SerializeField, Min(0f)] private float pingPongSpeed = 50f;

    [Header("Score Effect")]
    [Tooltip("A non-temporary ScoreModifierProperty of type IncomeMultiplier. It is active only inside the profitable range.")]
    [SerializeField] private Key_ScoreModifierPP incomeMultiplierModifierId = Key_ScoreModifierPP.FurnaceHeatIncomeMultiplier;

    private float currentHeat;
    private int calibrationDirection = -1;
    private bool isUnlocked;
    private bool isCalibrating;
    private ScoreManager modifierTarget;
    private bool modifierAppliedToTarget;
    private bool appliedModifierState;

    /// <summary>Raised after the heat value, calibration state, or unlock state changes.</summary>
    public event Action OnHeatStateChanged;

    public float CurrentHeat => currentHeat;
    public float NormalizedHeat => currentHeat / 100f;
    public float ProfitableHeatMin => Mathf.Min(profitableHeatMin, profitableHeatMax);
    public float ProfitableHeatMax => Mathf.Max(profitableHeatMin, profitableHeatMax);
    public float SecondsPerHeatPoint => Mathf.Max(MinimumSecondsPerPoint, secondsPerHeatPoint);
    public float PingPongSpeed => Mathf.Max(0f, pingPongSpeed);
    public bool IsUnlocked => isUnlocked;
    public bool IsCalibrating => isCalibrating;
    public bool IsInProfitableRange => isUnlocked && currentHeat >= ProfitableHeatMin && currentHeat <= ProfitableHeatMax;
    public Key_ScoreModifierPP IncomeMultiplierModifierId => incomeMultiplierModifierId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentHeat = Mathf.Clamp(initialHeat, 0f, 100f);
        isUnlocked = startsUnlocked;
    }

    private void Start()
    {
        SynchronizeRuntimeModifier();
        OnHeatStateChanged?.Invoke();
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
        initialHeat = Mathf.Clamp(initialHeat, 0f, 100f);
        profitableHeatMin = Mathf.Clamp(profitableHeatMin, 0f, 100f);
        profitableHeatMax = Mathf.Clamp(profitableHeatMax, 0f, 100f);
        secondsPerHeatPoint = Mathf.Max(MinimumSecondsPerPoint, secondsPerHeatPoint);
        pingPongSpeed = Mathf.Max(0f, pingPongSpeed);
    }

    private void Update()
    {
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager != null && scoreManager.IsSimulationPaused)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        bool heatChanged = false;
        if (isUnlocked)
        {
            heatChanged = isCalibrating
                ? AdvancePingPong(deltaTime)
                : AdvanceCooling(deltaTime);
        }

        SynchronizeRuntimeModifier();

        if (heatChanged)
            OnHeatStateChanged?.Invoke();
    }

    /// <summary>
    /// Starts manual PingPong calibration, or locks the current pointer and resumes natural cooling.
    /// Entering calibration always begins by travelling toward lower heat values.
    /// </summary>
    public bool ToggleManualSet()
    {
        if (!isUnlocked)
            return false;

        isCalibrating = !isCalibrating;
        if (isCalibrating)
            calibrationDirection = -1;

        SynchronizeRuntimeModifier();
        OnHeatStateChanged?.Invoke();
        return true;
    }

    /// <summary>Reserved for the later unlock system. Disabling heat also removes its score effect.</summary>
    public void SetUnlocked(bool unlocked)
    {
        if (isUnlocked == unlocked)
            return;

        isUnlocked = unlocked;
        if (!isUnlocked)
            isCalibrating = false;

        SynchronizeRuntimeModifier();
        OnHeatStateChanged?.Invoke();
    }

    private bool AdvanceCooling(float deltaTime)
    {
        float nextHeat = Mathf.Max(0f, currentHeat - (deltaTime / SecondsPerHeatPoint));
        if (Mathf.Approximately(nextHeat, currentHeat))
            return false;

        currentHeat = nextHeat;
        return true;
    }

    private bool AdvancePingPong(float deltaTime)
    {
        float remainingDistance = PingPongSpeed * deltaTime;
        if (remainingDistance <= 0f)
            return false;

        float previousHeat = currentHeat;
        while (remainingDistance > 0f)
        {
            float boundary = calibrationDirection < 0 ? 0f : 100f;
            float distanceToBoundary = Mathf.Abs(boundary - currentHeat);

            if (remainingDistance < distanceToBoundary)
            {
                currentHeat += calibrationDirection * remainingDistance;
                break;
            }

            currentHeat = boundary;
            remainingDistance -= distanceToBoundary;
            calibrationDirection *= -1;

            // A pointer that starts exactly on a boundary must immediately head toward the other end.
            if (distanceToBoundary <= 0f && remainingDistance <= 0f)
                break;
        }

        return !Mathf.Approximately(previousHeat, currentHeat);
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

        bool shouldBeActive = IsInProfitableRange;
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
}
