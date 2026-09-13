using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation-only HUD for the authoritative values owned by <see cref="ScoreManager"/>.
/// It refreshes when the score state changes and does not mutate any gameplay value.
/// </summary>
public class StatControllerUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI incomePerSecondText;
    [SerializeField] private TextMeshProUGUI nangAmountText;
    [SerializeField] private TextMeshProUGUI unitPriceText;
    [SerializeField] private TextMeshProUGUI autoNangPerSecondText;
    [SerializeField] private HUDStatRollAnimator statRollAnimator;

    private ScoreManager scoreManager;

    private void Start()
    {
        TryResolveScoreManager();
        Refresh();
        StartCoroutine(RefreshAfterScoreManagerInitialization());
    }

    private void Update()
    {
        // The ScoreManager may be initialized after this HUD in a future scene setup.
        // Once found, the OnScoreChanged event handles all subsequent refreshes.
        if (scoreManager == null && TryResolveScoreManager())
            Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    [ContextMenu("Refresh Stats")]
    public void Refresh()
    {
        if (!TryResolveScoreManager())
            return;

        if (moneyText != null)
            moneyText.text = FormatValue(scoreManager.Money);

        if (nangAmountText != null)
            nangAmountText.text = FormatValue(scoreManager.NangAmt);

        string unitPrice = FormatValue(scoreManager.UnitSalePrice);
        string autoNangPerSecond = FormatValue(scoreManager.FinalAutoNangPerSec) + "/s";
        string incomePerSecond = FormatValue(scoreManager.IncomePerSecond) + "/s";

        if (statRollAnimator != null)
        {
            statRollAnimator.ApplyValues(unitPrice, autoNangPerSecond, incomePerSecond);
            return;
        }

        // Keep a direct-display fallback so an intentionally animation-free HUD still works if
        // this presentation component is removed from a different scene.
        if (incomePerSecondText != null)
            incomePerSecondText.text = incomePerSecond;

        if (unitPriceText != null)
            unitPriceText.text = unitPrice;

        if (autoNangPerSecondText != null)
            autoNangPerSecondText.text = autoNangPerSecond;
    }

    private bool TryResolveScoreManager()
    {
        if (scoreManager != null)
            return true;

        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
            return false;

        scoreManager.OnScoreChanged += Refresh;
        return true;
    }

    private void Unsubscribe()
    {
        if (scoreManager != null)
            scoreManager.OnScoreChanged -= Refresh;

        scoreManager = null;
    }

    private IEnumerator RefreshAfterScoreManagerInitialization()
    {
        // ScoreManager calculates its initial modifier state during Start. Refresh once more on
        // the next frame so this display is correct regardless of script execution order.
        yield return null;
        Refresh();
    }

    private static string FormatValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "\u2014";

        double magnitude = Math.Abs(value);
        const double Million = 1_000_000d;
        const double Billion = 1_000_000_000d;
        const double Trillion = 1_000_000_000_000d;

        if (magnitude >= Trillion)
            return (value / Trillion).ToString("0.##", CultureInfo.InvariantCulture) + "T";
        if (magnitude >= Billion)
            return (value / Billion).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        if (magnitude >= Million)
            return (value / Million).ToString("0.##", CultureInfo.InvariantCulture) + "M";

        return value.ToString(magnitude >= 100d ? "N0" : "0.##", CultureInfo.InvariantCulture);
    }
}
