using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    [CustomEditor(typeof(HexEnemyManager))]
    public sealed class HexEnemyManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            HexEnemyManager manager = (HexEnemyManager)target;

            if (GUILayout.Button("Rebuild BFS Distance Field"))
            {
                manager.RebuildDistanceField();
                EditorUtility.SetDirty(manager);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Configured Enemy Wave"))
                    manager.SpawnConfiguredWaveNow();

                if (GUILayout.Button("Advance Enemy Step"))
                    manager.AdvanceEnemyStepNow();

                if (GUILayout.Button("Apply Enemy Hurt Step"))
                    manager.ApplyHurtStepNow();

                if (GUILayout.Button("Reset Enemy Simulation"))
                    manager.ResetEnemySimulation();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Navigation Ready", manager.NavigationReady.ToString());
            EditorGUILayout.LabelField("Reachable Cells", manager.ReachableCellCount.ToString());
            EditorGUILayout.LabelField("Spawn Candidates", manager.SpawnCandidateCount.ToString());
            EditorGUILayout.LabelField("Enemy Stacks", manager.StackCount.ToString());
            EditorGUILayout.LabelField("Enemy Count", manager.TotalEnemyCount.ToString());
            EditorGUILayout.LabelField("Target Points", manager.TargetPointsRemaining.ToString());
            EditorGUILayout.LabelField("Reached Count", manager.TotalReachedCount.ToString());
        }
    }
}
