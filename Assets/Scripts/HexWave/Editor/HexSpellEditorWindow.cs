using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    public sealed class HexSpellEditorWindow : EditorWindow
    {
        private enum Brush { Select, Region, EraseRegion, Impulse, Wall }
        [SerializeField] private HexSpellProperty spell;
        [SerializeField] private int gridSize = 13;
        [SerializeField] private int direction;
        [SerializeField] private float playbackSpeed = 1f;
        private Brush brush;
        private HexCoordinate selected;
        private float paintImpulse = 8f, paintTime;
        private readonly HashSet<HexCoordinate> walls = new HashSet<HexCoordinate>();
        private readonly List<HexSpellWave> waves = new List<HexSpellWave>();
        private bool playing, loop;
        private double lastUpdate, elapsed;
        private float accumulator;
        private Vector2 scroll;
        private PreviewRenderUtility preview;
        private Mesh mesh;
        private Material material;
        private MaterialPropertyBlock block;
        private HexWaveManager settingsSource;
        private int observedSpellHash;
        private const float StepTime = 0.02f;

        [MenuItem("Tools/Hex Wave/Spell Editor")]
        public static void OpenMenu() => Open(null);
        public static void Open(HexSpellProperty target)
        {
            var window = GetWindow<HexSpellEditorWindow>("Hex Spell Editor");
            if (target != null) window.spell = target;
            if (window.settingsSource == null) window.settingsSource = FindFirstObjectByType<HexWaveManager>();
            window.minSize = new Vector2(1050, 650); window.Show(); window.ResetPreview();
        }
        private void OnEnable()
        { lastUpdate = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; Undo.undoRedoPerformed += ResetPreview; }
        private void OnDisable()
        {
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= ResetPreview;
            preview?.Cleanup(); preview = null;
            if (mesh != null) DestroyImmediate(mesh); if (material != null) DestroyImmediate(material);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                spell = (HexSpellProperty)EditorGUILayout.ObjectField(spell, typeof(HexSpellProperty), false, GUILayout.Width(280));
                int nextSize = EditorGUILayout.IntSlider("Grid k × k", gridSize, 5, 25, GUILayout.Width(250));
                if (EditorGUI.EndChangeCheck()) { gridSize = nextSize; ResetPreview(); }
                if (GUILayout.Button("Rotate 60°", EditorStyles.toolbarButton)) { direction = (direction + 1) % 6; ResetPreview(); }
                GUILayout.Label($"Direction {direction + 1}/6", GUILayout.Width(100));
                if (GUILayout.Button("Save Asset", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
            }
            if (spell == null) { EditorGUILayout.HelpBox("Select a HexSpellProperty. Scene objects are never modified by this window.", MessageType.Info); return; }
            int hash = EditorJsonUtility.ToJson(spell).GetHashCode();
            if (hash != observedSpellHash) { ResetPreview(); observedSpellHash = hash; }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(315))) DrawInspector();
                using (new EditorGUILayout.VerticalScope())
                {
                    brush = (Brush)GUILayout.Toolbar((int)brush, new[] { "Select", "Region +", "Region −", "Impulse +", "Test Wall" });
                    GUILayout.Label("Blue: allowed region   Red/purple: impulses   White: anchor/heading   Gray: test walls", EditorStyles.miniLabel);
                    Rect mapRect = GUILayoutUtility.GetRect(300, Mathf.Max(240, position.height * 0.43f), GUILayout.ExpandWidth(true));
                    DrawMap(mapRect);
                    DrawPlayback();
                    Rect previewRect = GUILayoutUtility.GetRect(300, 180, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    if (Event.current.type == EventType.Repaint) Draw3D(previewRect);
                }
            }
        }

        private void DrawInspector()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Spell configuration", EditorStyles.boldLabel);
            var data = new SerializedObject(spell); data.Update();
            foreach (string name in new[] { "enumKey", "displayName", "description", "icon", "strength", "edgeMode", "edgeDamping", "absorptionWidth" })
                EditorGUILayout.PropertyField(data.FindProperty(name));
            if (data.ApplyModifiedProperties()) ResetPreview();
            if (!spell.Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Anchor is always (0,0,0).", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Selected local offset", selected.ToString());
            paintImpulse = EditorGUILayout.FloatField("Paint impulse", paintImpulse);
            paintTime = Mathf.Max(0f, EditorGUILayout.FloatField("Paint time (seconds)", paintTime));
            if (GUILayout.Button("Add event at selected offset")) AddImpulse(selected);
            for (int i = 0; i < spell.impulses.Count; i++)
            {
                if (spell.impulses[i].offset != selected) continue;
                var hit = spell.impulses[i];
                EditorGUILayout.LabelField($"Event {i + 1}", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                float time = Mathf.Max(0, EditorGUILayout.FloatField("Time", hit.time));
                float impulse = EditorGUILayout.FloatField("Impulse", hit.impulse);
                if (EditorGUI.EndChangeCheck())
                { Undo.RecordObject(spell, "Edit spell event"); spell.impulses[i] = new HexSpellImpulse(selected, time, impulse); Changed(); }
                if (GUILayout.Button("Delete this event"))
                { Undo.RecordObject(spell, "Delete spell event"); spell.impulses.RemoveAt(i); Changed(); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.Space();
            settingsSource = (HexWaveManager)EditorGUILayout.ObjectField("Physics settings source", settingsSource, typeof(HexWaveManager), true);
            EditorGUILayout.HelpBox("Physics uses this map's parameters, or defaults (m=1, k=20/35, c=1.8/0.6). Test walls affect only this preview. Changing a wall restarts the preview.", MessageType.Info);
            if (GUILayout.Button("Use scene physics settings")) { settingsSource = FindFirstObjectByType<HexWaveManager>(); ResetPreview(); }
            if (GUILayout.Button("Clear test walls")) { walls.Clear(); ResetPreview(); }
            EditorGUILayout.LabelField($"Region: {spell.region.Count} cells   Events: {spell.impulses.Count}");
            EditorGUILayout.EndScrollView();
        }

        private void DrawMap(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.055f, 0.075f, 0.105f));
            float scale = Mathf.Min(rect.width / (gridSize * 1.0f), rect.height / (gridSize * 1.5f));
            Vector2 center = rect.center;
            var allowed = new HashSet<HexCoordinate>(spell.region);
            Handles.BeginGUI();
            int min = -gridSize / 2;
            for (int a = min; a < min + gridSize; a++)
            for (int b = min; b < min + gridSize; b++)
            {
                var worldCoordinate = HexCoordinate.FromAxial(a, b);
                var offset = worldCoordinate.Rotate(-direction);
                Vector3 local = worldCoordinate.ToLocalPosition(1f);
                Vector2 p = center + new Vector2(local.x, -local.z) * scale;
                bool inRegion = allowed.Contains(offset), wall = walls.Contains(worldCoordinate);
                float impulse = 0; bool hasHit = false;
                foreach (var hit in spell.impulses) if (hit.offset == offset) { impulse += hit.impulse * spell.strength; hasHit = true; }
                Color color = wall ? new Color(0.25f, 0.28f, 0.32f) : hasHit ? (impulse >= 0 ? new Color(0.75f, 0.12f, 0.08f) : new Color(0.5f, 0.15f, 0.7f)) :
                    inRegion ? new Color(0.05f, 0.25f, 0.45f) : new Color(0.1f, 0.13f, 0.17f);
                var points = new Vector3[7];
                for (int c = 0; c <= 6; c++)
                { float angle = c * Mathf.PI / 3f; points[c] = p + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * scale / Mathf.Sqrt(3f) * 0.94f; }
                Handles.color = color; Handles.DrawAAConvexPolygon(points);
                Handles.color = offset == selected ? Color.white : inRegion ? new Color(0.2f, 0.65f, 1f) : new Color(0.3f, 0.35f, 0.4f);
                Handles.DrawAAPolyLine(offset == selected ? 3f : 1f, points);
                if (worldCoordinate == HexCoordinate.Origin) GUI.Label(new Rect(p.x - 10, p.y - 10, 40, 20), "000", EditorStyles.whiteMiniLabel);
                else if (hasHit) GUI.Label(new Rect(p.x - 14, p.y - 8, 50, 20), wall ? "×" : impulse.ToString("+0.#;-0.#;0"), EditorStyles.whiteMiniLabel);
            }
            var heading = HexCoordinate.GetNeighborDirection(1).Rotate(direction).ToLocalPosition(1);
            Handles.color = Color.white;
            Vector3 end = center + new Vector2(heading.x, -heading.z) * scale * 1.5f;
            Handles.DrawAAPolyLine(3f, center, end);
            Handles.EndGUI();
            var current = Event.current;
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && rect.Contains(current.mousePosition))
            {
                Vector2 p = (current.mousePosition - center) / scale;
                var coordinate = HexCoordinate.FromLocalPosition(new Vector3(p.x, 0, -p.y), 1);
                if (coordinate.A >= min && coordinate.A < min + gridSize && coordinate.B >= min && coordinate.B < min + gridSize)
                {
                    selected = coordinate.Rotate(-direction);
                    if (brush == Brush.Region && !allowed.Contains(selected))
                    { Undo.RecordObject(spell, "Paint spell region"); spell.region.Add(selected); Changed(); }
                    else if (brush == Brush.EraseRegion && allowed.Contains(selected))
                    { Undo.RecordObject(spell, "Erase spell region"); spell.region.Remove(selected); spell.impulses.RemoveAll(x => x.offset == selected); Changed(); }
                    else if (brush == Brush.Impulse && current.type == EventType.MouseDown) AddImpulse(selected);
                    else if (brush == Brush.Wall && current.type == EventType.MouseDown)
                    { if (!walls.Add(coordinate)) walls.Remove(coordinate); ResetPreview(); }
                }
                current.Use(); Repaint();
            }
        }

        private void AddImpulse(HexCoordinate offset)
        {
            if (float.IsNaN(paintImpulse) || float.IsInfinity(paintImpulse)) return;
            Undo.RecordObject(spell, "Add spell event");
            if (!spell.region.Contains(offset)) spell.region.Add(offset);
            spell.impulses.Add(new HexSpellImpulse(offset, paintTime, paintImpulse)); Changed();
        }
        private void Changed() { EditorUtility.SetDirty(spell); ResetPreview(); }
        private void ResetPreview() { waves.Clear(); elapsed = 0; accumulator = 0; playing = false; lastUpdate = EditorApplication.timeSinceStartup; Repaint(); }
        private HexWaveSettings Settings => settingsSource != null ? settingsSource.SimulationSettings : new HexWaveSettings
        { mass = 1, kOrigin = 20, cOrigin = 1.8f, kNeighbor = 35, cNeighbor = 0.6f, sleepHeight = 0.0005f, sleepVelocity = 0.0005f, sleepSteps = 12, minimumSubsteps = 1 };
        private bool Movable(HexCoordinate cell)
        {
            int min = -gridSize / 2;
            return cell.A > min && cell.A < min + gridSize - 1 && cell.B > min && cell.B < min + gridSize - 1 && !walls.Contains(cell);
        }
        private bool AddCast()
        {
            if (spell == null || !spell.Validate(out _) || !Movable(HexCoordinate.Origin)) return false;
            var wave = new HexSpellWave(spell, HexCoordinate.Origin, direction, 1, Settings, Movable);
            if (wave.IsFinished) return false;
            waves.Add(wave); return true;
        }
        private void Advance()
        {
            foreach (var wave in waves) wave.Step(StepTime);
            waves.RemoveAll(x => x.IsFinished); elapsed += StepTime;
            if (waves.Count == 0) { if (loop) AddCast(); else playing = false; }
        }
        private void DrawPlayback()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton))
                { if (playing) playing = false; else { if (waves.Count == 0) AddCast(); playing = waves.Count > 0; lastUpdate = EditorApplication.timeSinceStartup; } }
                if (GUILayout.Button("Replay", EditorStyles.toolbarButton)) { ResetPreview(); playing = AddCast(); }
                if (GUILayout.Button("Step 0.02s", EditorStyles.toolbarButton)) { playing = false; if (waves.Count == 0) AddCast(); Advance(); }
                if (GUILayout.Button("Add Cast", EditorStyles.toolbarButton)) { AddCast(); }
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton);
                playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.1f, 3f, GUILayout.Width(140));
                GUILayout.Label($"{elapsed:0.00}s   Waves {waves.Count}", GUILayout.Width(140));
            }
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing)
            {
                accumulator += (float)System.Math.Min(0.1, now - lastUpdate) * playbackSpeed;
                int guard = 0;
                while (accumulator >= StepTime && playing && guard++ < 20) { Advance(); accumulator -= StepTime; }
                Repaint();
            }
            lastUpdate = now;
        }

        private void Draw3D(Rect rect)
        {
            if (rect.width < 1 || rect.height < 1) return;
            if (preview == null)
            {
                preview = new PreviewRenderUtility(); preview.camera.fieldOfView = 35;
                preview.camera.clearFlags = CameraClearFlags.Color; preview.camera.backgroundColor = new Color(0.3f, 0.35f, 0.42f);
                preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                preview.lights[1].intensity = 0.7f;
                mesh = BuildColumn();
                material = new Material(Shader.Find("NangClicker/Hex Spell Preview")); block = new MaterialPropertyBlock();
            }
            preview.BeginPreview(rect, GUIStyle.none);
            var camera = preview.camera;
            float extent = gridSize * 0.95f;
            camera.transform.position = new Vector3(extent * 0.35f, extent * 1.15f, -extent * 1.3f);
            camera.transform.LookAt(new Vector3(0, 0.3f, 0)); camera.nearClipPlane = 0.05f; camera.farClipPlane = extent * 6f;
            int min = -gridSize / 2;
            for (int a = min; a < min + gridSize; a++)
            for (int b = min; b < min + gridSize; b++)
            {
                var cell = HexCoordinate.FromAxial(a, b); float height = 0;
                foreach (var wave in waves) height += wave.GetHeight(cell);
                var offset = cell.Rotate(-direction); bool hit = spell.impulses.Exists(x => x.offset == offset);
                Color color = !Movable(cell) ? new Color(0.36f, 0.4f, 0.46f) : hit ? new Color(1f, 0.3f, 0.1f) :
                    spell.region.Contains(offset) ? Color.Lerp(new Color(0.12f, 0.65f, 0.95f), new Color(0.3f, 0.9f, 1f), Mathf.Clamp01(Mathf.Abs(height))) : new Color(0.58f, 0.66f, 0.72f);
                if (cell == HexCoordinate.Origin) color = Color.white;
                block.SetColor("_BaseColor", color);
                preview.DrawMesh(mesh, Matrix4x4.TRS(cell.ToLocalPosition(1, height), Quaternion.identity, Vector3.one), material, 0, block);
            }
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private static Mesh BuildColumn()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int side = 0; side < 6; side++)
            {
                float a = side * Mathf.PI / 3, b = (side + 1) * Mathf.PI / 3;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.55f;
                var q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * 0.55f;
                int i = vertices.Count; vertices.Add(Vector3.up); vertices.Add(p + Vector3.up); vertices.Add(q + Vector3.up);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                i = vertices.Count; vertices.Add(p); vertices.Add(q); vertices.Add(q + Vector3.up); vertices.Add(p + Vector3.up);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }
            var result = new Mesh { name = "Preview Hex Column" }; result.SetVertices(vertices); result.SetTriangles(triangles, 0); result.RecalculateNormals(); return result;
        }
    }

    [CustomEditor(typeof(HexSpellProperty))]
    public sealed class HexSpellPropertyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Spell Editor / Live Preview")) HexSpellEditorWindow.Open((HexSpellProperty)target);
            DrawDefaultInspector();
            if (!((HexSpellProperty)target).Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
