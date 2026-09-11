using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Presentation-only view of the authoritative score formula and its already-aggregated factors.
/// It never enumerates individual modifiers or recalculates score values.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScoreFormulaDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI formulaText;

    [SerializeField] private GameObject debugPanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.O;

    public bool IsOpen => debugPanel != null && debugPanel.activeSelf;

    private ScoreManager scoreManager;

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        Refresh();
        StartCoroutine(RefreshAfterScoreManagerInitialization());
    }

    private void Update()
    {
        // ScoreManager may be initialized after this HUD in a future scene setup.
        if (scoreManager == null)
            Refresh();

        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();   
        }
    }

    public void Toggle()
    {
        if (debugPanel == null)
        {
            Debug.LogWarning($"[{nameof(ScoreFormulaDebugUI)}] No debubg panel is assigned.", this);
            return;
        }

        debugPanel.SetActive(!debugPanel.activeSelf);
    }
    private void OnDisable()
    {
        Unsubscribe();
    }

    [ContextMenu("Refresh Formula")]
    public void Refresh()
    {
        if (formulaText == null || !TryResolveScoreManager())
            return;

        formulaText.text =
            "Profit Formula\n" +
            "Income / Sec = ((((1 + a) \u00D7 b \u00D7 Base Price) + c) \u00D7 (1 + d) \u00D7 e) \u00D7 Nang / Sec\n" +
            $"a = {FormatPercent(scoreManager.UnitSalePriceAdditiveRate)}    b = {FormatMultiplier(scoreManager.UnitSalePriceMultiplier)}    c = {FormatSigned(scoreManager.UnitSalePriceFlatBonus)}\n" +
            $"d = {FormatPercent(scoreManager.IncomeAdditiveRate)}    e = {FormatMultiplier(scoreManager.IncomeMultiplier)}\n" +
            $"Base Price = {FormatNumber(scoreManager.BaseUnitSalePrice)}    Nang / Sec = {FormatNumber(scoreManager.NangPerSecond)}\n" +
            $"Final Sale / Nang = {FormatNumber(scoreManager.FinalSaleValuePerNang)}\n" +
            $"Income / Sec = {FormatNumber(scoreManager.IncomePerSecond)}";
    }

    private IEnumerator RefreshAfterScoreManagerInitialization()
    {
        // ScoreManager recalculates its initial values during Start. Refresh once on the next
        // frame so this view is correct regardless of script execution order.
        yield return null;
        Refresh();
    }

    private bool TryResolveScoreManager()
    {
        if (scoreManager != null)
            return true;

        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
            return false;

        scoreManager.OnScoreChanged += Refresh;
        scoreManager.OnModifiersChanged += Refresh;
        return true;
    }

    private void Unsubscribe()
    {
        if (scoreManager == null)
            return;

        scoreManager.OnScoreChanged -= Refresh;
        scoreManager.OnModifiersChanged -= Refresh;
        scoreManager = null;
    }

    private static string FormatNumber(double value)
    {
        return IsFinite(value)
            ? value.ToString("0.####", CultureInfo.InvariantCulture)
            : "\u2014";
    }

    private static string FormatPercent(double rate)
    {
        return IsFinite(rate)
            ? (rate * 100d).ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture) + "%"
            : "\u2014";
    }

    private static string FormatMultiplier(double multiplier)
    {
        return IsFinite(multiplier)
            ? "\u00D7" + FormatNumber(multiplier)
            : "\u2014";
    }

    private static string FormatSigned(double value)
    {
        return IsFinite(value)
            ? value.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture)
            : "\u2014";
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
