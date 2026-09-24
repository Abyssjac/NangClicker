using UnityEditor;
using UnityEngine;

/// <summary>Play Mode test controls shared by all puzzle targets.</summary>
[CustomEditor(typeof(BasePuzzleTarget), true)]
public sealed class PuzzleTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
            return;

        BasePuzzleTarget puzzleTarget = (BasePuzzleTarget)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Test", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Trigger"))
                puzzleTarget.SetTrigger(puzzleTarget, true);

            if (GUILayout.Button("Untrigger"))
                puzzleTarget.SetTrigger(puzzleTarget, false);

            if (GUILayout.Button("Reset"))
                puzzleTarget.ResetPuzzle();
        }
    }
}
