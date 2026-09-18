using UnityEditor;
using UnityEngine;

namespace MonaLisaGame.Editor
{
    /// <summary>Play-mode utility window for creating real photo points without changing scene data.</summary>
    public sealed class PhotoPointManagerDebugWindow : EditorWindow
    {
        private int debugSpawnCount = 1;

        [MenuItem("Mona Lisa/Photo Point Debug")]
        public static void ShowWindow() => GetWindow<PhotoPointManagerDebugWindow>("Photo Point Debug").Show();

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            PhotoPointManager manager = FindFirstObjectByType<PhotoPointManager>();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("No PhotoPointManager exists in the active scene.", MessageType.Warning);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Spawn controls are available only in Play Mode and never edit scene data.", MessageType.Info);
                return;
            }

            PhotoPointManager.DebugSnapshot snapshot = manager.GetDebugSnapshot();
            EditorGUILayout.LabelField("Live State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Points", snapshot.ActivePointCount.ToString());
            EditorGUILayout.LabelField("Pooled Points", snapshot.PooledPointCount.ToString());
            EditorGUILayout.LabelField("Success", snapshot.SuccessCount.ToString());
            EditorGUILayout.LabelField("Failure", snapshot.FailureCount.ToString());

            EditorGUILayout.Space();
            debugSpawnCount = Mathf.Max(0, EditorGUILayout.IntField("Points to Spawn", debugSpawnCount));
            if (GUILayout.Button("Spawn Points in Gaze Area"))
                manager.SpawnDebugPoints(debugSpawnCount);

            if (GUILayout.Button("Clear Active Points (No Count Change)"))
                manager.ClearAllPhotoPoints();

            if (GUILayout.Button("Reset Success / Failure Counts"))
                manager.ResetCounts();
        }
    }
}
