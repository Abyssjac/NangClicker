using UnityEditor;
using UnityEngine;

/// <summary>Play Mode controls and diagnostics for a level's color-puzzle activation manager.</summary>
public sealed class PuzzleColorActivationManagerWindow : EditorWindow
{
    private static readonly PuzzleColorType[] ManagedColors =
    {
        PuzzleColorType.PuzzleRed,
        PuzzleColorType.PuzzleBlue,
        PuzzleColorType.PuzzleGreen,
    };

    private PuzzleColorActivationManager activationManager;

    [MenuItem("Tools/Puzzle System/Color Activation Manager")]
    private static void OpenWindow()
    {
        PuzzleColorActivationManagerWindow window = GetWindow<PuzzleColorActivationManagerWindow>();
        window.titleContent = new GUIContent("Color Puzzles");
        window.minSize = new Vector2(310f, 250f);
        window.FindManagerIfNeeded();
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        FindManagerIfNeeded();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Puzzle Color Activation", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        activationManager = (PuzzleColorActivationManager)EditorGUILayout.ObjectField(
            "Manager",
            activationManager,
            typeof(PuzzleColorActivationManager),
            true);

        if (GUILayout.Button("Find", GUILayout.Width(52f)))
            FindManager();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to test color channels. This window never changes scene state while editing.",
                MessageType.Info);
            return;
        }

        FindManagerIfNeeded();
        if (activationManager == null)
        {
            EditorGUILayout.HelpBox(
                "No PuzzleColorActivationManager was found in the loaded scenes.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "These controls use the manager's manual test source. Other active sources can keep a color enabled.",
            MessageType.None);

        foreach (PuzzleColorType colorType in ManagedColors)
            DrawColorRow(colorType);

        EditorGUILayout.Space();
        if (GUILayout.Button("Clear All Color Sources", GUILayout.Height(26f)))
            activationManager.ClearAllColors();
    }

    private void DrawColorRow(PuzzleColorType colorType)
    {
        bool isActive = activationManager.IsColorActive(colorType);
        int sourceCount = activationManager.GetActiveSourceCount(colorType);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(colorType.ToString(), GUILayout.Width(115f));
        EditorGUILayout.LabelField(isActive ? "Active" : "Inactive", GUILayout.Width(62f));
        EditorGUILayout.LabelField($"Sources: {sourceCount}", GUILayout.Width(78f));

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = GetColor(colorType);
        if (GUILayout.Button(isActive ? "Deactivate" : "Activate"))
            activationManager.SetManualColorActive(colorType, !isActive);
        GUI.backgroundColor = previousColor;

        EditorGUILayout.EndHorizontal();
    }

    private void FindManagerIfNeeded()
    {
        if (activationManager == null)
            FindManager();
    }

    private void FindManager()
    {
        PuzzleColorActivationManager[] managers = Object.FindObjectsByType<PuzzleColorActivationManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        activationManager = managers.Length > 0 ? managers[0] : null;
        Repaint();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange stateChange)
    {
        if (stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.EnteredEditMode)
        {
            activationManager = null;
            FindManagerIfNeeded();
            Repaint();
        }
    }

    private static Color GetColor(PuzzleColorType colorType)
    {
        switch (colorType)
        {
            case PuzzleColorType.PuzzleRed:
                return new Color(0.86f, 0.32f, 0.32f);
            case PuzzleColorType.PuzzleBlue:
                return new Color(0.31f, 0.55f, 0.92f);
            case PuzzleColorType.PuzzleGreen:
                return new Color(0.32f, 0.78f, 0.42f);
            default:
                return Color.white;
        }
    }
}
