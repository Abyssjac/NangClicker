using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    [CustomEditor(typeof(HexWaveManager))]
    public sealed class HexWaveManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            HexWaveManager manager = (HexWaveManager)target;

            if (GUILayout.Button("Rebuild Grid Topology"))
            {
                Undo.RecordObject(manager, "Rebuild Hex Wave Grid");
                manager.RebuildGrid();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Open Hex Grid Painter"))
                HexGridPainterWindow.Open(manager);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Impulse Origin"))
                    manager.EnqueueDefaultImpulse(HexCoordinate.Origin);

                if (GUILayout.Button("Reset Simulation"))
                    manager.ResetSimulation();
            }

            if (manager.CellPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a HexCellView prefab before painting cells.",
                    MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "Any cell missing one or more of its six neighbors is treated as a fixed boundary.",
                MessageType.None);
        }
    }
}
