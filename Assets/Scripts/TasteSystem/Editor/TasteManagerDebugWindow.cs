#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Play-mode debug controls for the authoritative TasteManager state.</summary>
public sealed class TasteManagerDebugWindow : EditorWindow
{
    private static readonly SpiceType[] AllSpices =
    {
        SpiceType.Sesame,
        SpiceType.Cheese,
        SpiceType.ChiliPowder,
        SpiceType.Cumin,
    };

    [MenuItem("Jacky Tools/Taste Manager")]
    public static void ShowWindow()
    {
        GetWindow<TasteManagerDebugWindow>("Taste Manager").Show();
    }

    private void Update()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        TasteManager manager = TasteManager.Instance;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Enter Play Mode with a TasteManager in the scene to use these controls.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Taste State", EditorStyles.boldLabel);
        DrawReadOnlyRow("Current Taste", FormatSpices(manager.CurrentSpices));
        DrawReadOnlyRow("Customer Preference", FormatSpices(manager.CurrentPreference));
        DrawReadOnlyRow("Exact Match", manager.IsExactTasteMatch ? "Yes" : "No");
        DrawReadOnlyRow("Income Multiplier Active", manager.IsIncomeMultiplierActive ? "Yes (x5)" : "No");

        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager != null)
            DrawReadOnlyRow("Current e", scoreManager.IncomeMultiplier.ToString("0.###"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Taste", EditorStyles.boldLabel);
        foreach (SpiceType spice in AllSpices)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(manager.CurrentSpices.Contains(spice) || !manager.IsSpiceUnlocked(spice)))
                {
                    if (GUILayout.Button("Add " + spice))
                        manager.AddSpice(spice);
                }

                using (new EditorGUI.DisabledScope(!manager.CurrentSpices.Contains(spice)))
                {
                    if (GUILayout.Button("Remove " + spice))
                        manager.RemoveSpice(spice);
                }
            }
        }

        using (new EditorGUI.DisabledScope(manager.CurrentSpices.Count == 0))
        {
            if (GUILayout.Button("Clear All Spices"))
                manager.ClearSpice();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Customer Preference", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh Random Non-Empty Preference"))
            manager.RefreshPreference();

        EditorGUILayout.HelpBox(
            "Refresh is manual for now. A match requires exactly the same spices, independent of add order. " +
            "The multiplier is ScoreModifierType.IncomeMultiplier, not a separate taste formula type.",
            MessageType.None);
    }

    private static void DrawReadOnlyRow(string label, string value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(170f));
            EditorGUILayout.LabelField(value);
        }
    }

    private static string FormatSpices(IReadOnlyList<SpiceType> spices)
    {
        if (spices == null || spices.Count == 0)
            return "(None)";

        List<string> labels = new();
        for (int index = 0; index < spices.Count; index++)
            labels.Add(spices[index].ToString());
        return string.Join(", ", labels);
    }
}
#endif
