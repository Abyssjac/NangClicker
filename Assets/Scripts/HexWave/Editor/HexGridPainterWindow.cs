using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    public sealed class HexGridPainterWindow : EditorWindow
    {
        private enum PaintMode
        {
            Add,
            Erase
        }

        [SerializeField] private HexWaveManager manager;
        [SerializeField] private PaintMode paintMode;

        [MenuItem("Tools/Hex Wave/Grid Painter")]
        public static void OpenFromMenu()
        {
            Open(FindFirstObjectByType<HexWaveManager>());
        }

        public static void Open(HexWaveManager targetManager)
        {
            HexGridPainterWindow window = GetWindow<HexGridPainterWindow>("Hex Grid Painter");
            window.manager = targetManager;
            window.Show();
            window.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (manager == null)
                return;

            manager.RebuildGrid();
            EditorUtility.SetDirty(manager);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            manager = (HexWaveManager)EditorGUILayout.ObjectField(
                "Wave Manager",
                manager,
                typeof(HexWaveManager),
                true);

            paintMode = (PaintMode)GUILayout.Toolbar((int)paintMode, new[] { "Add", "Erase" });

            if (manager == null)
            {
                EditorGUILayout.HelpBox("Assign a HexWaveManager in the active scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Spacing", manager.Spacing.ToString("0.###"));
            EditorGUILayout.ObjectField("Cell Prefab", manager.CellPrefab, typeof(HexCellView), false);

            if (manager.CellPrefab == null)
                EditorGUILayout.HelpBox("The manager needs a cell prefab before Add mode can paint.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Move the cursor in Scene view and left-click or drag. Hold Alt to use normal Scene view navigation.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Topology And Boundary"))
            {
                manager.RebuildGrid();
                EditorUtility.SetDirty(manager);
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (manager == null || Application.isPlaying)
                return;

            Event currentEvent = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            Plane gridPlane = new Plane(manager.transform.up, manager.transform.position);
            if (!gridPlane.Raycast(ray, out float enter))
                return;

            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 localPoint = manager.CellsRoot.InverseTransformPoint(worldPoint);
            HexCoordinate coordinate = HexCoordinate.FromLocalPosition(localPoint, manager.Spacing);
            DrawPreview(coordinate);

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            bool paintEvent =
                (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag) &&
                currentEvent.button == 0 &&
                !currentEvent.alt;

            if (!paintEvent)
                return;

            if (paintMode == PaintMode.Add)
                AddCell(coordinate);
            else
                EraseCell(coordinate);

            currentEvent.Use();
        }

        private void AddCell(HexCoordinate coordinate)
        {
            if (manager.CellPrefab == null || FindCell(coordinate) != null)
                return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                manager.CellPrefab.gameObject,
                manager.CellsRoot);

            if (instance == null)
                return;

            Undo.RegisterCreatedObjectUndo(instance, "Paint Hex Cell");
            instance.name = $"Hex {coordinate.A}_{coordinate.B}_{coordinate.C}";

            HexCellView cell = instance.GetComponent<HexCellView>();
            cell.SetCoordinate(coordinate);
            cell.transform.localPosition = coordinate.ToLocalPosition(manager.Spacing);
            cell.transform.localRotation = Quaternion.identity;
            cell.CaptureBaseLocalPosition();

            EditorUtility.SetDirty(cell);
            FinalizeGridChange();
        }

        private void EraseCell(HexCoordinate coordinate)
        {
            HexCellView cell = FindCell(coordinate);
            if (cell == null)
                return;

            Undo.DestroyObjectImmediate(cell.gameObject);
            FinalizeGridChange();
        }

        private HexCellView FindCell(HexCoordinate coordinate)
        {
            HexCellView[] cells = manager.CellsRoot.GetComponentsInChildren<HexCellView>(true);
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Coordinate == coordinate)
                    return cells[i];
            }

            return null;
        }

        private void FinalizeGridChange()
        {
            manager.RebuildGrid();
            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void DrawPreview(HexCoordinate coordinate)
        {
            Vector3 localCenter = coordinate.ToLocalPosition(manager.Spacing);
            float radius = manager.Spacing / Mathf.Sqrt(3f) * 0.94f;
            Vector3[] points = new Vector3[7];

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                points[i] = manager.CellsRoot.TransformPoint(localCenter + localOffset);
            }

            points[6] = points[0];
            Handles.color = paintMode == PaintMode.Add
                ? new Color(0.15f, 1f, 0.35f, 0.9f)
                : new Color(1f, 0.2f, 0.1f, 0.9f);
            Handles.DrawAAPolyLine(3f, points);
        }
    }
}
