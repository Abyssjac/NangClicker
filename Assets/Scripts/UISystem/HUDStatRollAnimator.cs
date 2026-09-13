using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation-only rolling-number effect for the three derived HUD statistics.
/// It never owns or changes score data; <see cref="StatControllerUI"/> supplies formatted values.
/// </summary>
[DisallowMultipleComponent]
public sealed class HUDStatRollAnimator : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI unitPriceText;
    [SerializeField] private TextMeshProUGUI autoNangPerSecondText;
    [SerializeField] private TextMeshProUGUI incomePerSecondText;

    [Header("Rolling Animation")]
    [Tooltip("Total time for the changed primary value(s) to resolve from right to left.")]
    [SerializeField, Min(0.05f)] private float rollDuration = 2f;
    [Tooltip("How long Income / Sec remains masked before the rolling values begin resolving.")]
    [SerializeField, Min(0f)] private float incomeMaskLeadDuration = 0.12f;
    [Tooltip("How frequently unresolved digits change while they are rolling.")]
    [SerializeField, Min(0.01f)] private float digitSpinInterval = 0.05f;

    private bool isInitialized;
    private int animationVersion;
    private string lastUnitPrice = string.Empty;
    private string lastAutoNangPerSecond = string.Empty;
    private string lastIncomePerSecond = string.Empty;

    /// <summary>
    /// Applies already-formatted HUD text. Initial values are shown immediately. Later Unit Price
    /// and Auto Nang / Sec changes restart the effect immediately with the newest values.
    /// </summary>
    public void ApplyValues(string unitPrice, string autoNangPerSecond, string incomePerSecond)
    {
        unitPrice ??= string.Empty;
        autoNangPerSecond ??= string.Empty;
        incomePerSecond ??= string.Empty;

        if (!isInitialized)
        {
            SetImmediate(unitPrice, autoNangPerSecond, incomePerSecond);
            return;
        }

        bool unitPriceChanged = unitPrice != lastUnitPrice;
        bool autoNangPerSecondChanged = autoNangPerSecond != lastAutoNangPerSecond;
        bool incomePerSecondChanged = incomePerSecond != lastIncomePerSecond;

        lastUnitPrice = unitPrice;
        lastAutoNangPerSecond = autoNangPerSecond;
        lastIncomePerSecond = incomePerSecond;

        if (!unitPriceChanged && !autoNangPerSecondChanged)
        {
            // Income-only changes (for example, a multiplicative condition changing) do not
            // receive their own special animation. A primary roll already in progress continues.
            if (incomePerSecondChanged && incomePerSecondText != null)
                incomePerSecondText.text = incomePerSecond;

            return;
        }

        RestartPrimaryRoll(unitPriceChanged, autoNangPerSecondChanged);
    }

    private void OnDisable()
    {
        if (!isInitialized)
            return;

        animationVersion++;
        StopAllCoroutines();
        WriteFinalValues();
    }

    private void SetImmediate(string unitPrice, string autoNangPerSecond, string incomePerSecond)
    {
        animationVersion++;
        StopAllCoroutines();

        isInitialized = true;
        lastUnitPrice = unitPrice;
        lastAutoNangPerSecond = autoNangPerSecond;
        lastIncomePerSecond = incomePerSecond;
        WriteFinalValues();
    }

    private void RestartPrimaryRoll(bool unitPriceChanged, bool autoNangPerSecondChanged)
    {
        animationVersion++;
        int currentVersion = animationVersion;
        StopAllCoroutines();

        if (unitPriceText != null && !unitPriceChanged)
            unitPriceText.text = lastUnitPrice;

        if (autoNangPerSecondText != null && !autoNangPerSecondChanged)
            autoNangPerSecondText.text = lastAutoNangPerSecond;

        if (incomePerSecondText != null)
            incomePerSecondText.text = BuildMaskedValue(lastIncomePerSecond);

        float resolvedDuration = Mathf.Max(0.05f, rollDuration);
        if (unitPriceChanged && unitPriceText != null)
            StartCoroutine(RollDigits(unitPriceText, lastUnitPrice, resolvedDuration, currentVersion));

        if (autoNangPerSecondChanged && autoNangPerSecondText != null)
            StartCoroutine(RollDigits(autoNangPerSecondText, lastAutoNangPerSecond, resolvedDuration, currentVersion));

        StartCoroutine(RevealIncomeAfterPrimaryRoll(resolvedDuration, currentVersion));
    }

    private IEnumerator RollDigits(TextMeshProUGUI text, string targetValue, float duration, int version)
    {
        int digitCount = CountDigits(targetValue);
        if (digitCount == 0)
        {
            text.text = targetValue;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (version != animationVersion)
                yield break;

            text.text = BuildRollingValue(targetValue, elapsed, duration, digitCount);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (version == animationVersion)
            text.text = targetValue;
    }

    private IEnumerator RevealIncomeAfterPrimaryRoll(float duration, int version)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (version != animationVersion)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (version != animationVersion)
            yield break;

        if (incomePerSecondText != null)
            incomePerSecondText.text = lastIncomePerSecond;
    }

    private void WriteFinalValues()
    {
        if (unitPriceText != null)
            unitPriceText.text = lastUnitPrice;

        if (autoNangPerSecondText != null)
            autoNangPerSecondText.text = lastAutoNangPerSecond;

        if (incomePerSecondText != null)
            incomePerSecondText.text = lastIncomePerSecond;
    }

    private string BuildRollingValue(string targetValue, float elapsed, float duration, int digitCount)
    {
        char[] characters = targetValue.ToCharArray();
        float leadDuration = Mathf.Min(Mathf.Max(0f, incomeMaskLeadDuration), duration);
        float resolveDuration = Mathf.Max(0.0001f, duration - leadDuration);
        float resolveProgress = Mathf.Clamp01((elapsed - leadDuration) / resolveDuration);
        int resolvedDigitCount = Mathf.FloorToInt(resolveProgress * digitCount);
        int frame = Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, digitSpinInterval));
        int digitRankFromRight = 0;

        for (int i = characters.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(characters[i]))
                continue;

            if (digitRankFromRight >= resolvedDigitCount)
            {
                int rollingDigit = PositiveModulo((frame * 7) + (digitRankFromRight * 3), 10);
                characters[i] = (char)('0' + rollingDigit);
            }

            digitRankFromRight++;
        }

        return new string(characters);
    }

    private static string BuildMaskedValue(string value)
    {
        char[] characters = value.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsDigit(characters[i]))
                characters[i] = '?';
        }

        return new string(characters);
    }

    private static int CountDigits(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
                count++;
        }

        return count;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
