using System;
using JackyUtility;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode inspection and presentation-only crowd controls for StoreVisualController.
/// None of these controls write to ScoreManager or the serialized crowd-tier configuration.
/// </summary>
public sealed class StoreVisualControllerDebugWindow : DebugEditorWindow<StoreVisualController>
{
    private bool hasInitialisedControls;
    private bool useAutomaticRateOverride;
    private double automaticRateOverride;

    [MenuItem("Jacky Tools/Store Visual Controller")]
    public static void ShowWindow() =>
        GetWindow<StoreVisualControllerDebugWindow>("Store Visual Controller").Show();

    private void Update()
    {
        if (Application.isPlaying)
            Repaint();
    }

    protected override void DrawContent()
    {
        StoreVisualController controller = UnityEngine.Object.FindFirstObjectByType<StoreVisualController>();
        if (controller == null)
            controller = Target;

        if (controller == null)
        {
            EditorGUILayout.HelpBox("No StoreVisualController instance found.", MessageType.Warning);
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Crowd monitoring and overrides are available in Play Mode. They are runtime-only and never change ScoreManager or scene data.",
                MessageType.Info);
            return;
        }

        StoreVisualController.CrowdDebugSnapshot snapshot = controller.GetCrowdDebugSnapshot();
        InitialiseControls(snapshot);

        DrawLiveState(snapshot);
        DrawRateOverride(controller);
        DrawUtilityControls(controller);
    }

    private void DrawLiveState(StoreVisualController.CrowdDebugSnapshot snapshot)
    {
        Header("Live Crowd State");
        Row("Automatic Nang / Sec", snapshot.AutomaticNangPerSecond.ToString("0.###"));
        Row("Rate Source", snapshot.UsesAutomaticRateOverride ? "Debug Override" : "ScoreManager");
        Row("Current Tier", snapshot.TierIndex < 0 ? "None" : (snapshot.TierIndex + 1).ToString());
        Row("Tier Threshold", snapshot.TierIndex < 0 ? "-" : snapshot.TierThreshold.ToString("0.###"));
        Row("Spawn Interval", snapshot.TierIndex < 0 ? "-" : snapshot.SpawnIntervalSeconds.ToString("0.###") + " s");
        Row("Spawn Chance", snapshot.TierIndex < 0 ? "-" : "95%");
        Row("Active Customers", snapshot.ActiveCustomerCount.ToString());
        Row("Pooled Customers", snapshot.PooledCustomerCount.ToString());
        Row("Next Spawn", FormatTimer(snapshot.NextSpawnInSeconds));
    }

    private void DrawRateOverride(StoreVisualController controller)
    {
        Header("Automatic Rate Test");
        EditorGUILayout.HelpBox(
            "Override only changes StoreVisualController's tier sampling. ScoreManager's automatic production remains unchanged.",
            MessageType.Info);

        bool requestedOverride = EditorGUILayout.Toggle("Use Rate Override", useAutomaticRateOverride);
        automaticRateOverride = EditorGUILayout.DoubleField("Automatic Nang / Sec", automaticRateOverride);
        automaticRateOverride = double.IsNaN(automaticRateOverride) || double.IsInfinity(automaticRateOverride)
            ? 0d
            : Math.Max(0d, automaticRateOverride);

        if (requestedOverride != useAutomaticRateOverride)
        {
            useAutomaticRateOverride = requestedOverride;
            if (useAutomaticRateOverride)
                controller.SetDebugAutomaticNangPerSecondOverride(automaticRateOverride);
            else
                controller.ClearDebugAutomaticNangPerSecondOverride();
        }

        using (new EditorGUI.DisabledScope(!useAutomaticRateOverride))
        {
            if (GUILayout.Button("Apply Automatic Rate"))
                controller.SetDebugAutomaticNangPerSecondOverride(automaticRateOverride);
        }
    }

    private static void DrawUtilityControls(StoreVisualController controller)
    {
        Header("Utility");
        if (GUILayout.Button("Force Tier Refresh"))
            controller.ForceRefreshCrowdForDebug();

        if (GUILayout.Button("Clear Active Customers"))
            controller.ClearCustomersForDebug();
    }

    private void InitialiseControls(StoreVisualController.CrowdDebugSnapshot snapshot)
    {
        if (hasInitialisedControls)
            return;

        hasInitialisedControls = true;
        useAutomaticRateOverride = snapshot.UsesAutomaticRateOverride;
        automaticRateOverride = snapshot.AutomaticNangPerSecond;
    }

    private static string FormatTimer(float timer)
    {
        return timer < 0f ? "Not scheduled" : timer.ToString("0.###") + " s";
    }
}
