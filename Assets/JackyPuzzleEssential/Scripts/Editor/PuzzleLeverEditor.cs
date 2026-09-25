using UnityEditor;
using UnityEngine;

/// <summary>Inspector controls for exercising a lever without a player interaction implementation.</summary>
[CustomEditor(typeof(PuzzleLever))]
public sealed class PuzzleLeverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
            return;

        PuzzleLever lever = (PuzzleLever)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode Test", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!lever.CanInteract))
        {
            if (GUILayout.Button("Try Interact"))
                lever.TryInteract();
        }

        if (GUILayout.Button("Reset"))
            lever.ResetPuzzle();
    }
}
