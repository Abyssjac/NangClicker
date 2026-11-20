using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    public sealed class HexSpellEditorWindow : EditorWindow
    {
        private enum Brush { Select, Region, EraseRegion, Event, Wall, Enemy }
        [SerializeField] private HexSpellProperty spell;
        [SerializeField] private int gridSize = 13, direction;
        [SerializeField] private float playbackSpeed = 1;
        [SerializeField] private HexEditorCanvasView editView = new HexEditorCanvasView();
        [SerializeField] private Vector3 previewPivot = new Vector3(0, 0.3f, 0);
        [SerializeField] private float previewDistance = 24;
        [SerializeField] private List<HexTrainingTarget> targets = new List<HexTrainingTarget>();
        [SerializeField] private HexBoundarySettings paintBoundary = HexBoundarySettings.Fixed;
        [SerializeField] private HexWaveManager settingsSource;
        [SerializeField] private HexEnemyManager damageSource;
        [SerializeField] private HexDamageSettings customDamage = HexDamageSettings.Default;
        private Brush brush;
        private HexCoordinate selected;
        private float paintImpulse = 8, paintTime, paintHeight = 0.8f;
        private HexSpellEventMode paintMode;
        private int selectedEdge;
        private readonly Dictionary<HexCoordinate, HexBoundarySettings> walls = new Dictionary<HexCoordinate, HexBoundarySettings>();
        private readonly Dictionary<HexCoordinate, HexBoundarySource> wallSources = new Dictionary<HexCoordinate, HexBoundarySource>();
        private readonly List<HexSpellWave> waves = new List<HexSpellWave>();
        private bool playing, loop, showDamage = true;
        private double lastUpdate, elapsed, nextDamageTime;
        private float accumulator;
        private Vector2 scroll;
        private PreviewRenderUtility preview;
        private Mesh mesh, dummyMesh;
        private Material material;
        private MaterialPropertyBlock block;
        private int observedSpellHash;
        private Rect lastMapRect, lastPreviewRect;
        private const float StepTime = 0.02f;
        private static readonly string[] EdgeNames = { "0 North", "1 North East", "2 South East", "3 South", "4 South West", "5 North West" };

        [MenuItem("Tools/Hex Wave/Spell Editor")]
        public static void OpenMenu() => Open(null);
        public static void Open(HexSpellProperty target)
        {
            var window = GetWindow<HexSpellEditorWindow>("Hex Spell Editor");
            if (target != null) window.spell = target;
            if (window.settingsSource == null) window.settingsSource = FindFirstObjectByType<HexWaveManager>();
            if (window.damageSource == null) window.damageSource = FindFirstObjectByType<HexEnemyManager>();
            window.minSize = new Vector2(1100, 720); window.Show(); window.ResetPreview();
        }
        private void OnEnable()
        {
            if (editView == null) editView = new HexEditorCanvasView();
            lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick; Undo.undoRedoPerformed += ResetPreview;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= ResetPreview;
            preview?.Cleanup(); preview = null;
            if (mesh != null) DestroyImmediate(mesh);
            if (dummyMesh != null) DestroyImmediate(dummyMesh);
            if (material != null) DestroyImmediate(material);
        }
        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.D &&
                focusedWindow == this && !EditorGUIUtility.editingTextField)
            { showDamage = !showDamage; Event.current.Use(); Repaint(); }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                spell = (HexSpellProperty)EditorGUILayout.ObjectField(spell, typeof(HexSpellProperty), false, GUILayout.Width(245));
                gridSize = EditorGUILayout.IntSlider("Grid k x k", gridSize, 5, 31, GUILayout.Width(240));
                if (EditorGUI.EndChangeCheck()) ResetPreview();
                if (GUILayout.Button("Rotate 60", EditorStyles.toolbarButton)) { direction = (direction + 1) % 6; ResetPreview(); }
                GUILayout.Label($"Direction {direction + 1}/6", GUILayout.Width(95));
                if (GUILayout.Button("Save Asset", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
            }
            if (spell == null) { EditorGUILayout.HelpBox("Select a HexSpellProperty. All simulation, walls and training targets are isolated from the game scene.", MessageType.Info); return; }
            int hash = EditorJsonUtility.ToJson(spell).GetHashCode();
            if (hash != observedSpellHash) { ResetPreview(); observedSpellHash = hash; }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(325))) DrawInspector();
                using (new EditorGUILayout.VerticalScope())
                {
                    brush = (Brush)GUILayout.Toolbar((int)brush, new[] { "Select", "Region +", "Region -", "Event +", "Wall", "Enemy" });
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("EDIT: wheel zoom / MMB pan", GUILayout.Width(210));
                        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton)) editView.Reset();
                        if (GUILayout.Button("Frame Spell", EditorStyles.toolbarButton)) FrameEdit(true);
                        if (GUILayout.Button("Frame Map", EditorStyles.toolbarButton)) FrameEdit(false);
                    }
                    GUILayout.Label("Blue region | Red/purple event | White anchor | Orange Amplify edge | Gray wall | Yellow enemy", EditorStyles.miniLabel);
                    lastMapRect = GUILayoutUtility.GetRect(300, Mathf.Max(230, position.height * 0.36f), GUILayout.ExpandWidth(true));
                    DrawMap(lastMapRect);
                    DrawPlayback();
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("PREVIEW: wheel zoom / MMB pan", GUILayout.Width(240));
                        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton)) { previewPivot = new Vector3(0, 0.3f, 0); previewDistance = gridSize * 1.85f; }
                        if (GUILayout.Button("Frame Spell", EditorStyles.toolbarButton)) Frame3D(true);
                        if (GUILayout.Button("Frame Map", EditorStyles.toolbarButton)) Frame3D(false);
                        showDamage = GUILayout.Toggle(showDamage, "Damage [D]", EditorStyles.toolbarButton);
                    }
                    Rect previewRect = GUILayoutUtility.GetRect(300, 180, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    lastPreviewRect = previewRect;
                    HandleNavigation(previewRect, false);
                    if (Event.current.type == EventType.Repaint) Draw3D(previewRect);
                }
            }
        }
        private void DrawInspector()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Spell data", EditorStyles.boldLabel);
            var data = new SerializedObject(spell); data.Update();
            foreach (string name in new[] { "enumKey", "displayName", "description", "icon", "strength", "chargeDuration", "holdDuration", "heightFromImpulse",
                "edgeMode", "edgeDamping", "absorptionWidth", "amplification" })
                EditorGUILayout.PropertyField(data.FindProperty(name), true);
            if (data.ApplyModifiedProperties()) ResetPreview();
            if (!spell.Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("Impulse: deltaV = impulse x strength / mass. Lift: authoritative target height x strength; no neighbor coupling before release. Release preserves height and adds no impulse.", MessageType.Info);
            EditorGUILayout.LabelField("Selected offset", selected.ToString());
            paintMode = (HexSpellEventMode)EditorGUILayout.EnumPopup("New event type", paintMode);
            paintTime = Mathf.Max(0, EditorGUILayout.FloatField("New start time", paintTime));
            if (paintMode == HexSpellEventMode.Impulse) paintImpulse = EditorGUILayout.FloatField("New impulse", paintImpulse);
            else paintHeight = EditorGUILayout.FloatField("New target height", paintHeight);
            if (GUILayout.Button("Add event at selected cell")) AddEvent(selected);
            for (int i = 0; i < spell.impulses.Count; i++)
            {
                var hit = spell.impulses[i]; if (hit.offset != selected) continue;
                EditorGUILayout.LabelField($"Event {i + 1}", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                hit.mode = (HexSpellEventMode)EditorGUILayout.EnumPopup("Type", hit.mode);
                hit.time = Mathf.Max(0, EditorGUILayout.FloatField("Start time", hit.time));
                if (hit.mode == HexSpellEventMode.Impulse) hit.impulse = EditorGUILayout.FloatField("Signed impulse", hit.impulse);
                else
                {
                    hit.heightFromStrength = EditorGUILayout.Toggle("Height from strength", hit.heightFromStrength);
                    if (hit.heightFromStrength) hit.impulse = EditorGUILayout.FloatField("Lift strength input", hit.impulse);
                    else hit.targetHeight = EditorGUILayout.FloatField("Signed target height", hit.targetHeight);
                    EditorGUILayout.LabelField("Final target", spell.LiftHeight(hit, 1).ToString("0.###"));
                    EditorGUILayout.LabelField("Release time", (hit.time + spell.chargeDuration + spell.holdDuration).ToString("0.###") + "s");
                }
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(spell, "Edit spell event"); spell.impulses[i] = hit; Changed(); }
                if (GUILayout.Button("Delete event"))
                { Undo.RecordObject(spell, "Delete event"); spell.impulses.RemoveAt(i); Changed(); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Boundary / wall brush response", EditorStyles.boldLabel);
            var windowData = new SerializedObject(this);
            EditorGUILayout.PropertyField(windowData.FindProperty("paintBoundary"), true);
            windowData.ApplyModifiedProperties();
            if (!paintBoundary.IsValid) EditorGUILayout.HelpBox("Boundary values must be finite; Amplify needs positive limits and a Fixed/Absorb fallback.", MessageType.Error);
            selectedEdge = EditorGUILayout.Popup("Selected local edge", selectedEdge, EdgeNames);
            bool outer = spell.region.Contains(selected) && !spell.region.Contains(selected.GetNeighbor(selectedEdge));
            using (new EditorGUI.DisabledScope(!outer || !paintBoundary.IsValid))
                if (GUILayout.Button("Set selected spell edge"))
                {
                    Undo.RecordObject(spell, "Set spell boundary");
                    spell.boundaryEdges.RemoveAll(x => x.offset == selected && x.direction == selectedEdge);
                    spell.boundaryEdges.Add(new HexSpellBoundaryEdge { offset = selected, direction = selectedEdge, response = paintBoundary }); Changed();
                }
            if (GUILayout.Button("Use default on selected edge"))
            { Undo.RecordObject(spell, "Reset spell edge"); spell.boundaryEdges.RemoveAll(x => x.offset == selected && x.direction == selectedEdge); Changed(); }
            var worldSelected = selected.Rotate(direction);
            if (walls.ContainsKey(worldSelected) && paintBoundary.IsValid && GUILayout.Button("Apply response to selected test wall"))
            { walls[worldSelected] = paintBoundary; ResetPreview(); }
            EditorGUILayout.HelpBox("An override applies to an outward edge of the blue region. Real scene walls take precedence. Amplify uses finite time, triggers and energy; expiry falls back without clearing waves.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            settingsSource = (HexWaveManager)EditorGUILayout.ObjectField("Physics source", settingsSource, typeof(HexWaveManager), true);
            damageSource = (HexEnemyManager)EditorGUILayout.ObjectField("Damage source", damageSource, typeof(HexEnemyManager), true);
            if (damageSource == null)
            {
                customDamage.minimumHeight = Mathf.Max(0, EditorGUILayout.FloatField("Damage height threshold", customDamage.minimumHeight));
                customDamage.heightScale = Mathf.Max(0, EditorGUILayout.FloatField("Damage height scale", customDamage.heightScale));
                customDamage.interval = Mathf.Max(0.01f, EditorGUILayout.FloatField("Hurt interval", customDamage.interval));
            }
            if (EditorGUI.EndChangeCheck()) ResetPreview();
            if (GUILayout.Button("Use scene physics settings")) { settingsSource = FindFirstObjectByType<HexWaveManager>(); ResetPreview(); }
            if (GUILayout.Button("Use scene damage settings")) { damageSource = FindFirstObjectByType<HexEnemyManager>(); ResetPreview(); }
            var physics = Settings; var damage = Damage;
            EditorGUILayout.HelpBox($"Physics: mass {physics.mass}, k {physics.kOrigin}/{physics.kNeighbor}, c {physics.cOrigin}/{physics.cNeighbor}.\nDamage: ceil(max(0, |h|-{damage.minimumHeight}) x {damage.heightScale}) x count, every {damage.interval}s.\nSources only supply parameters; no scene geometry is imported.", MessageType.Info);
            EditorGUILayout.LabelField("Training targets", EditorStyles.boldLabel);
            foreach (var target in targets)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(target.coordinate.ToString(), GUILayout.Width(95));
                    target.count = Mathf.Max(1, EditorGUILayout.IntField(target.count));
                }
                EditorGUILayout.LabelField($"Total {target.totalDamage}  Peak hit {target.highestHit}  Hits {target.hurtCount}");
                EditorGUILayout.LabelField($"Peak height {target.peakHeight:0.###}  Avg DPS {(elapsed > 0 ? target.totalDamage / elapsed : 0):0.##}");
            }
            if (GUILayout.Button("Clear training targets")) { targets.Clear(); ResetPreview(); }
            if (GUILayout.Button("Clear test walls")) { walls.Clear(); ResetPreview(); }
            EditorGUILayout.LabelField($"Region {spell.region.Count} / Events {spell.impulses.Count}");
            EditorGUILayout.EndScrollView();
        }

        private float BaseScale(Rect rect) => Mathf.Min(rect.width / gridSize, rect.height / (gridSize * 1.5f));
        private static Vector2 PlanePoint(HexCoordinate c) { var p = c.ToLocalPosition(1); return new Vector2(p.x, -p.z); }
        private void HandleNavigation(Rect rect, bool edit)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(edit ? 11909 : 11910, FocusType.Passive);
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                if (edit) editView.ZoomAt(e.mousePosition, rect, BaseScale(rect), e.delta.y);
                else previewDistance = Mathf.Clamp(previewDistance * Mathf.Exp(e.delta.y * 0.08f), 1.5f, 300);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDown && e.button == 2 && rect.Contains(e.mousePosition))
            { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                if (edit) editView.pan += e.delta;
                else
                {
                    Quaternion rotation = Quaternion.LookRotation(-new Vector3(0.35f, 1.15f, -1.3f));
                    Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
                    float units = previewDistance * 0.63f / Mathf.Max(1, rect.height);
                    previewPivot += (-right * e.delta.x + up * e.delta.y) * units;
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            { GUIUtility.hotControl = 0; e.Use(); }
        }
        private void DrawMap(Rect rect)
        {
            HandleNavigation(rect, true);
            EditorGUI.DrawRect(rect, new Color(0.055f, 0.075f, 0.105f));
            float scale = BaseScale(rect), radius = scale * editView.zoom / Mathf.Sqrt(3);
            var allowed = new HashSet<HexCoordinate>(spell.region);
            int min = -gridSize / 2;
            GUI.BeginClip(rect);
            Handles.BeginGUI();
            for (int a = min; a < min + gridSize; a++)
            for (int b = min; b < min + gridSize; b++)
            {
                var cell = HexCoordinate.FromAxial(a, b); var offset = cell.Rotate(-direction);
                Vector2 p = editView.ToScreen(PlanePoint(cell), rect, scale) - rect.position;
                if (p.x < -radius || p.x > rect.width + radius || p.y < -radius || p.y > rect.height + radius) continue;
                bool hit = spell.impulses.Exists(x => x.offset == offset), wall = walls.ContainsKey(cell);
                Color color = wall ? new Color(0.25f, 0.28f, 0.32f) : hit ? new Color(0.75f, 0.18f, 0.08f) :
                    allowed.Contains(offset) ? new Color(0.05f, 0.25f, 0.45f) : new Color(0.1f, 0.13f, 0.17f);
                var points = new Vector3[7];
                for (int c = 0; c <= 6; c++)
                { float angle = c * Mathf.PI / 3; points[c] = p + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * radius * 0.94f; }
                Handles.color = color; Handles.DrawAAConvexPolygon(points);
                Handles.color = offset == selected ? Color.white : new Color(0.3f, 0.35f, 0.4f);
                Handles.DrawAAPolyLine(offset == selected ? 3 : 1, points);
                if (allowed.Contains(offset)) for (int d = 0; d < 6; d++)
                    if (!allowed.Contains(offset.GetNeighbor(d)))
                    {
                        HexBoundarySettings response = spell.Boundary;
                        foreach (var edge in spell.boundaryEdges) if (edge.offset == offset && edge.direction == d) response = edge.response;
                        int corner = (7 - ((d + direction) % 6)) % 6;
                        Handles.color = BoundaryColor(response.mode); Handles.DrawAAPolyLine(3, points[corner], points[(corner + 1) % 6]);
                    }
                if (radius > 10)
                {
                    string label = cell == HexCoordinate.Origin ? "000" : hit ? EventLabel(offset) : "";
                    GUI.Label(new Rect(p.x - 18, p.y - 9, 65, 20), label, EditorStyles.whiteMiniLabel);
                }
                var target = targets.Find(x => x.coordinate == cell);
                if (target != null)
                {
                    Handles.color = Color.yellow; Handles.DrawWireDisc(new Vector3(p.x, p.y, 0), Vector3.forward, radius * 0.5f);
                    GUI.Label(new Rect(p.x - 30, p.y + 5, 110, 35), showDamage ? $"x{target.count}  {target.totalDamage}" : $"x{target.count}", EditorStyles.whiteMiniLabel);
                }
            }
            var center = editView.ToScreen(Vector2.zero, rect, scale) - rect.position;
            var end = editView.ToScreen(PlanePoint(HexCoordinate.GetNeighborDirection(1).Rotate(direction)) * 1.5f, rect, scale) - rect.position;
            Handles.color = Color.white; Handles.DrawAAPolyLine(3, center, end);
            Handles.EndGUI(); GUI.EndClip();
            var current = Event.current;
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && rect.Contains(current.mousePosition))
            {
                Vector2 p = editView.ToLocal(current.mousePosition, rect, scale);
                var cell = HexCoordinate.FromLocalPosition(new Vector3(p.x, 0, -p.y), 1);
                if (cell.A >= min && cell.A < min + gridSize && cell.B >= min && cell.B < min + gridSize)
                {
                    selected = cell.Rotate(-direction);
                    if (brush == Brush.Region && !allowed.Contains(selected))
                    { Undo.RecordObject(spell, "Paint region"); spell.region.Add(selected); CleanBoundaryEdges(); Changed(); }
                    else if (brush == Brush.EraseRegion && allowed.Contains(selected))
                    {
                        Undo.RecordObject(spell, "Erase region"); spell.region.Remove(selected); spell.impulses.RemoveAll(x => x.offset == selected);
                        spell.boundaryEdges.RemoveAll(x => x.offset == selected); Changed();
                    }
                    else if (brush == Brush.Event && current.type == EventType.MouseDown) AddEvent(selected);
                    else if (brush == Brush.Wall && current.type == EventType.MouseDown && paintBoundary.IsValid)
                    { if (!walls.Remove(cell)) walls.Add(cell, paintBoundary); ResetPreview(); }
                    else if (brush == Brush.Enemy && current.type == EventType.MouseDown)
                    {
                        var target = targets.Find(x => x.coordinate == cell);
                        if (target == null && Movable(cell)) targets.Add(new HexTrainingTarget { coordinate = cell });
                        else if (target != null) targets.Remove(target);
                        ResetPreview();
                    }
                }
                current.Use(); Repaint();
            }
        }
        private string EventLabel(HexCoordinate offset)
        {
            foreach (var hit in spell.impulses) if (hit.offset == offset)
                return hit.mode == HexSpellEventMode.Impulse ? (hit.impulse * spell.strength).ToString("+0.#;-0.#;0") : "H " + spell.LiftHeight(hit, 1).ToString("0.#");
            return "";
        }
        private static Color BoundaryColor(HexSpellEdgeMode mode) => mode == HexSpellEdgeMode.Amplify ? new Color(1, 0.55f, 0.08f) :
            mode == HexSpellEdgeMode.Absorb ? new Color(0.1f, 0.65f, 1) : mode == HexSpellEdgeMode.Free ? Color.green : Color.white;
        private void AddEvent(HexCoordinate offset)
        {
            if (!Finite(paintImpulse) || !Finite(paintHeight)) return;
            Undo.RecordObject(spell, "Add spell event"); if (!spell.region.Contains(offset)) spell.region.Add(offset);
            spell.impulses.Add(new HexSpellImpulse(offset, paintTime, paintImpulse) { mode = paintMode, targetHeight = paintHeight }); CleanBoundaryEdges(); Changed();
        }
        private void CleanBoundaryEdges() => spell.boundaryEdges.RemoveAll(x => !spell.region.Contains(x.offset) || spell.region.Contains(x.offset.GetNeighbor(x.direction)));
        private void Changed() { EditorUtility.SetDirty(spell); ResetPreview(); }
        private void ResetPreview()
        {
            waves.Clear(); wallSources.Clear(); elapsed = accumulator = 0; nextDamageTime = Mathf.Max(0.01f, Damage.interval); playing = false;
            foreach (var target in targets) target.ResetStats();
            lastUpdate = EditorApplication.timeSinceStartup; Repaint();
        }
        private HexWaveSettings Settings => settingsSource != null ? settingsSource.SimulationSettings : new HexWaveSettings
        { mass = 1, kOrigin = 20, cOrigin = 1.8f, kNeighbor = 35, cNeighbor = 0.6f, sleepHeight = 0.0005f, sleepVelocity = 0.0005f, sleepSteps = 12, minimumSubsteps = 1 };
        private HexDamageSettings Damage => damageSource != null ? damageSource.DamageSettings : customDamage;
        private bool Movable(HexCoordinate cell)
        {
            int min = -gridSize / 2;
            return cell.A > min && cell.A < min + gridSize - 1 && cell.B > min && cell.B < min + gridSize - 1 && !walls.ContainsKey(cell);
        }
        private HexBoundarySource MapBoundary(HexCoordinate cell)
        {
            if (!wallSources.TryGetValue(cell, out var source))
            { source = new HexBoundarySource(walls.TryGetValue(cell, out var response) ? response : HexBoundarySettings.Fixed, elapsed, true); wallSources.Add(cell, source); }
            return source;
        }
        private float Height(HexCoordinate cell) { float h = 0; foreach (var wave in waves) h += wave.GetHeight(cell); return h; }
        private bool AddCast()
        {
            if (spell == null || !spell.Validate(out _) || !Movable(HexCoordinate.Origin)) return false;
            var wave = new HexSpellWave(spell, HexCoordinate.Origin, direction, 1, Settings, Movable, MapBoundary, elapsed);
            if (wave.IsFinished) return false; waves.Add(wave); return true;
        }
        private void Advance()
        {
            foreach (var wave in waves) wave.Step(StepTime);
            waves.RemoveAll(x => x.IsFinished); elapsed += StepTime;
            foreach (var target in targets) target.peakHeight = Mathf.Max(target.peakHeight, Mathf.Abs(Height(target.coordinate)));
            while (elapsed + 1e-8 >= nextDamageTime)
            {
                foreach (var target in targets) if (Movable(target.coordinate)) target.Sample(Height(target.coordinate), Damage, nextDamageTime);
                nextDamageTime += Mathf.Max(0.01f, Damage.interval);
            }
            if (waves.Count == 0) { if (loop) AddCast(); else playing = false; }
        }
        private void DrawPlayback()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton))
                { if (playing) playing = false; else { if (waves.Count == 0) { ResetPreview(); AddCast(); } playing = waves.Count > 0; lastUpdate = EditorApplication.timeSinceStartup; } }
                if (GUILayout.Button("Replay", EditorStyles.toolbarButton)) { ResetPreview(); playing = AddCast(); }
                if (GUILayout.Button("Step", EditorStyles.toolbarButton)) { playing = false; if (waves.Count == 0) AddCast(); Advance(); }
                if (GUILayout.Button("Add Cast", EditorStyles.toolbarButton)) AddCast();
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton);
                playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.1f, 3, GUILayout.Width(130));
                GUILayout.Label($"{elapsed:0.00}s  Waves {waves.Count}", GUILayout.Width(125));
            }
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing)
            {
                accumulator += (float)Math.Min(0.1, now - lastUpdate) * playbackSpeed;
                int guard = 0; while (accumulator >= StepTime && playing && guard++ < 20) { Advance(); accumulator -= StepTime; }
                Repaint();
            }
            lastUpdate = now;
        }
        private Bounds ContentBounds(bool spellOnly)
        {
            bool any = false; var bounds = new Bounds(Vector3.zero, Vector3.one);
            if (spellOnly && spell != null)
                foreach (var offset in spell.region)
                { var p = offset.Rotate(direction).ToLocalPosition(1); if (!any) { bounds = new Bounds(p, Vector3.one); any = true; } else bounds.Encapsulate(p); }
            if (!any)
            {
                int min = -gridSize / 2;
                bounds = new Bounds(HexCoordinate.FromAxial(min, min).ToLocalPosition(1), Vector3.one);
                bounds.Encapsulate(HexCoordinate.FromAxial(min + gridSize - 1, min + gridSize - 1).ToLocalPosition(1));
            }
            // Fit both current displacement and authored lift targets, not only the resting ground plane.
            foreach (var wave in waves) for (int i = 0; i < wave.CellCount; i++)
                bounds.Encapsulate(wave.GetCoordinate(i).ToLocalPosition(1, wave.GetHeight(i) + 1));
            if (spell != null) foreach (var hit in spell.impulses)
                if (hit.mode == HexSpellEventMode.LiftAndRelease)
                    bounds.Encapsulate(hit.offset.Rotate(direction).ToLocalPosition(1, spell.LiftHeight(hit, 1) + 1));
            return bounds;
        }
        private void FrameEdit(bool spellOnly)
        {
            var bounds = ContentBounds(spellOnly);
            var rect = lastMapRect.width > 1 ? lastMapRect : new Rect(0, 0, position.width - 340, 300);
            float scale = BaseScale(rect);
            editView.zoom = Mathf.Clamp(Mathf.Min(rect.width / (bounds.size.x + 2), rect.height / (bounds.size.z + 2)) / scale, 0.12f, 12);
            editView.pan = -new Vector2(bounds.center.x, -bounds.center.z) * (scale * editView.zoom); Repaint();
        }
        private void Frame3D(bool spellOnly)
        {
            var bounds = ContentBounds(spellOnly); previewPivot = bounds.center + Vector3.up * 0.5f;
            previewDistance = Mathf.Max(4, bounds.size.magnitude * 1.45f + 3); Repaint();
        }
        private void Draw3D(Rect rect)
        {
            if (rect.width < 1 || rect.height < 1) return;
            if (preview == null)
            {
                preview = new PreviewRenderUtility(); preview.camera.fieldOfView = 35;
                mesh = BuildColumn(); dummyMesh = BuildDummy();
                material = new Material(Shader.Find("NangClicker/Hex Spell Preview")); block = new MaterialPropertyBlock();
            }
            preview.BeginPreview(rect, GUIStyle.none);
            var camera = preview.camera; camera.clearFlags = CameraClearFlags.Color; camera.backgroundColor = new Color(0.3f, 0.35f, 0.42f);
            Vector3 axis = new Vector3(0.35f, 1.15f, -1.3f).normalized;
            camera.transform.position = previewPivot + axis * previewDistance; camera.transform.LookAt(previewPivot);
            camera.nearClipPlane = 0.03f; camera.farClipPlane = Mathf.Max(200, previewDistance * 5);
            int min = -gridSize / 2;
            for (int a = min; a < min + gridSize; a++) for (int b = min; b < min + gridSize; b++)
            {
                var cell = HexCoordinate.FromAxial(a, b); float h = Height(cell); var offset = cell.Rotate(-direction);
                bool hit = spell.impulses.Exists(x => x.offset == offset);
                Color color = !Movable(cell) ? new Color(0.36f, 0.4f, 0.46f) : hit ? new Color(1, 0.3f, 0.1f) :
                    spell.region.Contains(offset) ? Color.Lerp(new Color(0.12f, 0.65f, 0.95f), new Color(0.3f, 0.9f, 1), Mathf.Clamp01(Mathf.Abs(h))) : new Color(0.58f, 0.66f, 0.72f);
                foreach (var wave in waves) if (wave.GetControl(cell) != HexMotionControl.Free) color = new Color(0.9f, 0.45f, 1);
                if (walls.TryGetValue(cell, out var response) && response.mode == HexSpellEdgeMode.Amplify) color = new Color(1, 0.6f, 0.1f);
                if (cell == HexCoordinate.Origin && Movable(cell)) color = Color.white;
                block.SetColor("_BaseColor", color);
                preview.DrawMesh(mesh, Matrix4x4.TRS(cell.ToLocalPosition(1, h), Quaternion.identity, Vector3.one), material, 0, block);
            }
            foreach (var target in targets)
            {
                if (!Movable(target.coordinate)) continue;
                block.SetColor("_BaseColor", new Color(1, 0.9f, 0.15f));
                preview.DrawMesh(dummyMesh, Matrix4x4.TRS(target.coordinate.ToLocalPosition(1, Height(target.coordinate) + 1.25f),
                    Quaternion.identity, Vector3.one * 0.38f), material, 0, block);
            }
            preview.Render(true); GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            if (showDamage) foreach (var target in targets)
            {
                if (!Movable(target.coordinate)) continue;
                Vector3 p = camera.WorldToViewportPoint(target.coordinate.ToLocalPosition(1, Height(target.coordinate) + 1.75f));
                if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) continue;
                GUI.Label(new Rect(rect.x + p.x * rect.width - 60, rect.y + (1 - p.y) * rect.height - 15, 180, 40),
                    $"x{target.count}  Total {target.totalDamage}\nPeak {target.highestHit}  Hits {target.hurtCount}", EditorStyles.whiteMiniLabel);
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Mesh BuildDummy()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); const int rings = 8, sides = 12;
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= sides; x++)
            { float latitude = y * Mathf.PI / rings, angle = x * 2 * Mathf.PI / sides; vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(angle), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(angle))); }
            for (int y = 0; y < rings; y++) for (int x = 0; x < sides; x++)
            { int a = y * (sides + 1) + x, b = a + sides + 1; triangles.Add(a); triangles.Add(a + 1); triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b); }
            var result = new Mesh { name = "Training Target Sphere" }; result.SetVertices(vertices); result.SetTriangles(triangles, 0); result.RecalculateNormals(); return result;
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
