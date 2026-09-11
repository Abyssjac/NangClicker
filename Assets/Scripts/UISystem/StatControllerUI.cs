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
    [SerializeField] private TextMeshProUGUI nangAmountText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI nangPerSecondText;

    [Header("Labels")]
    [SerializeField] private string nangAmountLabel = "Nang: ";
    [SerializeField] private string moneyLabel = "Money: ";
    [SerializeField] private string nangPerSecondLabel = "Nang / Sec: ";

    private ScoreManager scoreManager;

    private void Start()
    {
        TryResolveScoreManager();
        Refresh();
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

        if (nangAmountText != null)
            nangAmountText.text = nangAmountLabel + FormatValue(scoreManager.NangAmt);

        if (moneyText != null)
            moneyText.text = moneyLabel + FormatValue(scoreManager.Money);

        if (nangPerSecondText != null)
            nangPerSecondText.text = nangPerSecondLabel + FormatValue(GetDisplayedNangPerSecond());
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

    private double GetDisplayedNangPerSecond()
    {
        // Manual clicks are settled together with automatic production at the end of a tick.
        // After the first non-zero settlement, this is the actual Nang produced per second,
        // including the clicks performed in that production interval.
        if (scoreManager.LastTickTotalNang > 0d)
            return scoreManager.LastTickTotalNang / scoreManager.ProductionTickInterval;

        // Before the first settlement, show the known automatic rate rather than an empty zero.
        return scoreManager.NangPerSecond;
    }

    private static string FormatValue(double value)
    {
        const double Thousand = 1_000d;
        const double Million = 1_000_000d;
        const double Billion = 1_000_000_000d;
        const double Trillion = 1_000_000_000_000d;

        if (value >= Trillion)
            return (value / Trillion).ToString("0.##", CultureInfo.InvariantCulture) + "T";
        if (value >= Billion)
            return (value / Billion).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        if (value >= Million)
            return (value / Million).ToString("0.##", CultureInfo.InvariantCulture) + "M";
        if (value >= Thousand)
            return (value / Thousand).ToString("0.##", CultureInfo.InvariantCulture) + "K";

        return value.ToString(value >= 100d ? "0" : "0.##", CultureInfo.InvariantCulture);
    }
}
