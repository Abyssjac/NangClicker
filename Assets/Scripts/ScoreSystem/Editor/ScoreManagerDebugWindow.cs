using System.Collections.Generic;
using JackyUtility;
using UnityEditor;
using UnityEngine;

public class ScoreManagerDebugWindow : DebugEditorWindow<ScoreManager>
{
    private Key_ScoreModifierPP selectedPropertyId = Key_ScoreModifierPP.None;
    private bool useDurationOverride;
    private float durationOverride = 10f;
    private double manualNangToQueue = 1d;
    private double moneyToAdd = 100d;
    private double baseAutoNangPerSecondToAdd = 1d;

    [MenuItem("Jacky Tools/Score Manager")]
    public static void ShowWindow() =>
        GetWindow<ScoreManagerDebugWindow>("Score Manager").Show();

    protected override void DrawContent()
    {
        ScoreManager manager = ScoreManager.Instance != null ? ScoreManager.Instance : Target;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("No ScoreManager instance found.", MessageType.Warning);
            return;
        }

        DrawSimulationControls(manager);
        DrawScoreState(manager);
        DrawPositiveScoreAdjustments(manager);
        DrawProductionTick(manager);
        DrawFormula(manager);
        DrawPropertyTweakControls(manager);
        DrawPermanentModifierHistory(manager);
        DrawTemporaryModifiers(manager);
    }

    private void DrawSimulationControls(ScoreManager manager)
    {
        Header("Simulation");
        bool paused = EditorGUILayout.Toggle("Paused", manager.IsSimulationPaused);
        if (paused != manager.IsSimulationPaused)
            manager.SetSimulationPaused(paused);

        EditorGUILayout.HelpBox(
            "Paused stops production ticks and temporary-buff countdowns. Queued manual Nang is retained.",
            MessageType.Info);
    }

    private void DrawScoreState(ScoreManager manager)
    {
        Header("Authoritative State");
        Row("Money", Format(manager.Money));
        Row("Nang Amt", Format(manager.NangAmt));
        Row("Base Unit Sale Price", Format(manager.BaseUnitSalePrice));
        Row("Base Auto Nang / Sec", Format(manager.NangPerSecond));
        Row("Auto Nang Additive Bonus", Format(manager.AutoNangAdditiveBonus));
        Row("Final Auto Nang / Sec", Format(manager.FinalAutoNangPerSec));
        Row("Unit Sale Price", Format(manager.UnitSalePrice));
        Row("Final Sale Value / Nang", Format(manager.FinalSaleValuePerNang));
        Row("Income / Sec", Format(manager.IncomePerSecond));
    }

    private void DrawProductionTick(ScoreManager manager)
    {
        Header("Production Tick");
        Row("Tick Interval", manager.ProductionTickInterval.ToString("0.###") + " s");
        Row("Time Until Next Tick", manager.TimeUntilNextProductionTick.ToString("0.###") + " s");
        Row("Manual Nang / Click", Format(manager.ManualNangPerClick));
        Row("Pending Manual Nang", Format(manager.PendingManualNang));
        Row("Last Tick Automatic Nang", Format(manager.LastTickAutomaticNang));
        Row("Last Tick Manual Nang", Format(manager.LastTickManualNang));
        Row("Last Tick Total Nang", Format(manager.LastTickTotalNang));
        Row("Last Tick Income", Format(manager.LastTickIncome));

        if (GUILayout.Button("Queue One Manual Click"))
            manager.QueueManualNangClick();

        manualNangToQueue = EditorGUILayout.DoubleField("Manual Nang To Queue", manualNangToQueue);
        using (new EditorGUI.DisabledScope(manualNangToQueue <= 0d))
        {
            if (GUILayout.Button("Queue Manual Nang"))
                manager.QueueManualNangProduction(manualNangToQueue);
        }
    }

    private void DrawPositiveScoreAdjustments(ScoreManager manager)
    {
        Header("Add Score Values");
        EditorGUILayout.HelpBox(
            "These controls add positive amounts only. Final Auto Nang / Sec remains formula-derived and cannot be edited directly.",
            MessageType.Info);

        moneyToAdd = EditorGUILayout.DoubleField("Money To Add", moneyToAdd);
        using (new EditorGUI.DisabledScope(!IsPositiveFinite(moneyToAdd)))
        {
            if (GUILayout.Button("Add Money"))
                manager.AddMoney(moneyToAdd);
        }

        baseAutoNangPerSecondToAdd = EditorGUILayout.DoubleField(
            "Base Auto Nang / Sec To Add",
            baseAutoNangPerSecondToAdd);
        using (new EditorGUI.DisabledScope(!IsPositiveFinite(baseAutoNangPerSecondToAdd)))
        {
            if (GUILayout.Button("Add Base Auto Nang / Sec"))
                manager.AddNangPerSecond(baseAutoNangPerSecondToAdd);
        }
    }

    private void DrawFormula(ScoreManager manager)
    {
        Header("Formula Factors");
        Row("a - Unit Price Additive Rate", Format(manager.UnitSalePriceAdditiveRate));
        Row("b - Unit Price Multiplier", Format(manager.UnitSalePriceMultiplier));
        Row("c - Unit Price Flat Bonus", Format(manager.UnitSalePriceFlatBonus));
        Row("d - Income Additive Rate", Format(manager.IncomeAdditiveRate));
        Row("e - Income Multiplier", Format(manager.IncomeMultiplier));
        Row("Auto Nang Additive Amount", Format(manager.AutoNangAdditiveBonus));
    }

    private void DrawPropertyTweakControls(ScoreManager manager)
    {
        Header("Tweak Property");

        if (manager.ScoreModifierDatabase == null)
        {
            EditorGUILayout.HelpBox("ScoreModifierDatabase is missing.", MessageType.Warning);
            return;
        }

        selectedPropertyId = (Key_ScoreModifierPP)EditorGUILayout.EnumPopup(
            "Property Id", selectedPropertyId);

        ScoreModifierProperty property = manager.GetScoreModifierProperty(selectedPropertyId);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Select a Property Id that exists in ScoreModifierDatabase.", MessageType.Info);
            return;
        }

        EditorGUILayout.ObjectField("Property", property, typeof(ScoreModifierProperty), false);
        EditorGUILayout.LabelField("Display Name", property.DisplayName);
        EditorGUILayout.LabelField("Modifier", property.ModifierType.ToString());
        EditorGUILayout.LabelField("Amount", Format(property.Amount));

        if (property.IsTemporary)
        {
            EditorGUILayout.LabelField("Default Duration", property.DefaultDuration.ToString("0.###") + " s");
            useDurationOverride = EditorGUILayout.Toggle("Override Duration", useDurationOverride);
            if (useDurationOverride)
                durationOverride = EditorGUILayout.FloatField("Duration Override", durationOverride);
        }
        else
        {
            EditorGUILayout.LabelField("Lifetime", "Permanent");
            useDurationOverride = false;
        }

        if (GUILayout.Button("Tweak Selected Property"))
        {
            float overrideValue = property.IsTemporary && useDurationOverride
                ? durationOverride
                : -1f;
            manager.TweakScore(selectedPropertyId, overrideValue);
        }
    }

    private void DrawPermanentModifierHistory(ScoreManager manager)
    {
        Header("Permanent Modifier History");
        IReadOnlyList<Key_ScoreModifierPP> history = manager.PermanentModifierHistory;
        if (history.Count == 0)
        {
            EditorGUILayout.LabelField("None");
            return;
        }

        Dictionary<Key_ScoreModifierPP, int> stackCounts = new();
        for (int i = 0; i < history.Count; i++)
        {
            Key_ScoreModifierPP id = history[i];
            stackCounts.TryGetValue(id, out int count);
            stackCounts[id] = count + 1;
        }

        foreach (KeyValuePair<Key_ScoreModifierPP, int> pair in stackCounts)
        {
            ScoreModifierProperty property = manager.GetScoreModifierProperty(pair.Key);
            string label = property != null ? property.DisplayName : pair.Key.ToString();
            EditorGUILayout.LabelField(label, "Stacks: " + pair.Value);
        }
    }

    private void DrawTemporaryModifiers(ScoreManager manager)
    {
        Header("Temporary Modifiers");
        IReadOnlyDictionary<Key_ScoreModifierPP, float> activeModifiers =
            manager.TemporaryModifierRemainingTimes;

        if (activeModifiers.Count == 0)
        {
            EditorGUILayout.LabelField("None");
            return;
        }

        foreach (KeyValuePair<Key_ScoreModifierPP, float> pair in activeModifiers)
        {
            ScoreModifierProperty property = manager.GetScoreModifierProperty(pair.Key);
            string label = property != null ? property.DisplayName : pair.Key.ToString();
            EditorGUILayout.LabelField(label, pair.Value.ToString("0.###") + " s remaining");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###");
    }

    private static bool IsPositiveFinite(double value)
    {
        return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
