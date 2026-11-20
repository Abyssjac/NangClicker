using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    public sealed class HexSpellEditorWindow : EditorWindow
    {
        private enum Brush { Select, Region, Event, SpellWall, TestWall, Enemy }
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
        [SerializeField] private Key_HexBoundaryProfilePP wallBrush = Key_HexBoundaryProfilePP.Amplify;
        private readonly HashSet<HexCoordinate> stroke = new HashSet<HexCoordinate>();
        private readonly Dictionary<HexCoordinate, HexBoundarySettings> walls = new Dictionary<HexCoordinate, HexBoundarySettings>();
        private readonly Dictionary<HexCoordinate, HexBoundarySource> wallSources = new Dictionary<HexCoordinate, HexBoundarySource>();
        private HexWaveWorld world;
        private HexSpellWave previewCast;
        private bool playing, loop, showDamage = true;
        private bool metadataOpen = true, castOpen = true, boundaryOpen = true, eventOpen = true, environmentOpen, sourcesOpen, cleanupOpen;
        private string renderError;
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
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= ResetPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            playing = false; world = null; previewCast = null; DisposePreview();
        }
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            ResetPreview(); DisposePreview();
            if (state == PlayModeStateChange.EnteredEditMode) Repaint();
        }
        private void DisposePreview()
        {
            try { preview?.Cleanup(); } catch (Exception e) { Debug.LogWarning("Preview cleanup: " + e.Message); }
            preview = null;
            if (mesh != null) DestroyImmediate(mesh); mesh = null;
            if (dummyMesh != null) DestroyImmediate(dummyMesh); dummyMesh = null;
            if (material != null) DestroyImmediate(material); material = null;
            block = null; renderError = null;
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
                    brush = (Brush)GUILayout.Toolbar((int)brush, new[] { "Select", "Region", "Event", "Spell Wall", "Test Wall", "Enemy" });
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        GUILayout.Label("EDIT: wheel zoom / MMB pan", GUILayout.Width(210));
                        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton)) editView.Reset();
                        if (GUILayout.Button("Frame Spell", EditorStyles.toolbarButton)) FrameEdit(true);
                        if (GUILayout.Button("Frame Map", EditorStyles.toolbarButton)) FrameEdit(false);
                    }
                    GUILayout.Label("Blue region | Red/purple event | White anchor | + Amplify wall | - Absorb wall | Yellow enemy", EditorStyles.miniLabel);
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
                    if (!string.IsNullOrEmpty(renderError)) EditorGUI.HelpBox(previewRect, renderError, MessageType.Warning);
                }
            }
        }
        private void DrawInspector()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var data = new SerializedObject(spell); data.Update();
            metadataOpen = EditorGUILayout.Foldout(metadataOpen, "Identity / presentation", true);
            if (metadataOpen) foreach (string name in new[] { "enumKey", "stringKey", "displayName", "description", "icon" })
                EditorGUILayout.PropertyField(data.FindProperty(name), true);
            castOpen = EditorGUILayout.Foldout(castOpen, "Casting", true);
            if (castOpen)
            {
                foreach (string name in new[] { "strength", "chargeDuration", "wallDuration" }) EditorGUILayout.PropertyField(data.FindProperty(name));
                EditorGUILayout.HelpBox("Game: hold to charge; release when READY. Early release cancels. Preview automatically releases when ready.", MessageType.None);
            }
            boundaryOpen = EditorGUILayout.Foldout(boundaryOpen, "Region perimeter", true);
            if (boundaryOpen)
            {
                var mode = data.FindProperty("edgeMode");
                int index = mode.intValue == (int)HexSpellEdgeMode.Free ? 1 : mode.intValue == (int)HexSpellEdgeMode.Fixed ? 2 : 0;
                index = EditorGUILayout.Popup("Edge mode", index, new[] { "Absorb", "Free", "Fixed support" });
                mode.intValue = index == 1 ? 1 : index == 2 ? 2 : 0;
                EditorGUILayout.PropertyField(data.FindProperty("edgeDamping"));
                EditorGUILayout.PropertyField(data.FindProperty("absorptionWidth"));
                EditorGUILayout.HelpBox("Default response only at unoccupied perimeter edges. Wall cells take precedence.", MessageType.None);
            }
            if (data.ApplyModifiedProperties()) ResetPreview();
            if (!spell.Validate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            eventOpen = EditorGUILayout.Foldout(eventOpen, "Events at " + selected, true);
            if (eventOpen)
            {
                if (GUILayout.Button("Add event at selected cell")) AddEvent(selected);
                for (int i = 0; i < spell.impulses.Count; i++)
                {
                    var hit = spell.impulses[i]; if (hit.offset != selected) continue;
                    EditorGUILayout.LabelField("Event " + (i + 1), EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    hit.mode = (HexSpellEventMode)EditorGUILayout.EnumPopup("Type", hit.mode);
                    hit.releaseDelay = Mathf.Max(0, EditorGUILayout.FloatField("Release delay", hit.releaseDelay));
                    if (hit.mode == HexSpellEventMode.Impulse)
                    {
                        hit.impulse = EditorGUILayout.FloatField("Signed impulse", hit.impulse);
                        EditorGUILayout.LabelField("Final impulse", (hit.impulse * spell.strength).ToString("0.###"));
                    }
                    else
                    {
                        hit.targetHeight = EditorGUILayout.FloatField("Signed target height", hit.targetHeight);
                        EditorGUILayout.LabelField("Final target height", spell.LiftHeight(hit, 1).ToString("0.###"));
                    }
                    EditorGUILayout.LabelField("Executes after mouse release", hit.releaseDelay.ToString("0.###") + "s");
                    if (EditorGUI.EndChangeCheck())
                    { Undo.RecordObject(spell, "Edit spell event"); spell.impulses[i] = hit; Changed(); }
                    if (GUILayout.Button("Delete event"))
                    { Undo.RecordObject(spell, "Delete event"); spell.impulses.RemoveAt(i); Changed(); GUIUtility.ExitGUI(); }
                }
            }
            environmentOpen = EditorGUILayout.Foldout(environmentOpen, "Walls / training environment", true);
            if (environmentOpen)
            {
                wallBrush = (Key_HexBoundaryProfilePP)EditorGUILayout.EnumPopup("Spell wall response", wallBrush);
                int wallIndex = spell.walls.FindIndex(x => x.offset == selected);
                if (wallIndex >= 0)
                {
                    var w = spell.walls[wallIndex];
                    var response = (Key_HexBoundaryProfilePP)EditorGUILayout.EnumPopup("Selected wall response", w.response);
                    if (response != w.response)
                    { Undo.RecordObject(spell, "Change wall response"); w.response = response; spell.walls[wallIndex] = w; Changed(); }
                }
                var profile = HexSpellSceneSetup.ResolveProfile(wallBrush);
                using (new EditorGUI.DisabledScope(profile == null))
                    if (GUILayout.Button("Select shared response profile")) Selection.activeObject = profile;
                var windowData = new SerializedObject(this); windowData.Update();
                EditorGUILayout.PropertyField(windowData.FindProperty("paintBoundary"), true); windowData.ApplyModifiedProperties();
                var testCell = selected.Rotate(direction);
                if (walls.ContainsKey(testCell) && GUILayout.Button("Apply response to selected test wall"))
                { walls[testCell] = paintBoundary; ResetPreview(); }
                if (GUILayout.Button("Generate wall ring around region")) GenerateWallRing();
                foreach (var target in targets)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    { EditorGUILayout.LabelField(target.coordinate.ToString(), GUILayout.Width(100)); target.count = Mathf.Max(1, EditorGUILayout.IntField(target.count)); }
                    EditorGUILayout.LabelField($"Damage {target.totalDamage} | Peak hit {target.highestHit} | Hits {target.hurtCount}");
                    EditorGUILayout.LabelField($"Peak height {target.peakHeight:0.###} | Avg DPS {(elapsed > 0 ? target.totalDamage / elapsed : 0):0.##}");
                }
            }
            sourcesOpen = EditorGUILayout.Foldout(sourcesOpen, "Preview settings / damage", true);
            if (sourcesOpen)
            {
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
                var s = Settings; var d = Damage;
                EditorGUILayout.HelpBox($"Physics m={s.mass}, k={s.kOrigin}/{s.kNeighbor}, c={s.cOrigin}/{s.cNeighbor}\nSleep h={s.sleepHeight}, v={s.sleepVelocity}, steps={s.sleepSteps}\nDamage ceil(max(0, |h|-{d.minimumHeight}) x {d.heightScale}) x count, every {d.interval}s. Parameters only; scene geometry is not imported.", MessageType.None);
            }
            cleanupOpen = EditorGUILayout.Foldout(cleanupOpen, "Clear / reset tools", true);
            if (cleanupOpen)
            {
                if (GUILayout.Button("Clear training targets")) { targets.Clear(); ResetPreview(); }
                if (GUILayout.Button("Clear test walls")) { walls.Clear(); ResetPreview(); }
                if (GUILayout.Button("Clear spell walls")) { Undo.RecordObject(spell, "Clear spell walls"); spell.walls.Clear(); Changed(); }
            }
            EditorGUILayout.LabelField($"Region {spell.region.Count} / Events {spell.impulses.Count} / Walls {spell.walls.Count}");
            EditorGUILayout.EndScrollView();
        }
        private void GenerateWallRing()
        {
            if (wallBrush == Key_HexBoundaryProfilePP.None) return;
            Undo.RecordObject(spell, "Generate spell wall ring");
            var mask = new HashSet<HexCoordinate>(spell.region);
            var occupied = new HashSet<HexCoordinate>(); foreach (var w in spell.walls) occupied.Add(w.offset);
            foreach (var c in spell.region) for (int d = 0; d < 6; d++)
            {
                var n = c.GetNeighbor(d);
                if (!mask.Contains(n) && occupied.Add(n)) spell.walls.Add(new HexSpellWall { offset = n, response = wallBrush });
            }
            Changed();
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
                var authoredWall = spell.walls.FindIndex(x => x.offset == offset);
                if (authoredWall >= 0) wall = true;
                Color color = wall ? new Color(0.25f, 0.28f, 0.32f) : hit ? new Color(0.75f, 0.18f, 0.08f) :
                    allowed.Contains(offset) ? new Color(0.05f, 0.25f, 0.45f) : new Color(0.1f, 0.13f, 0.17f);
                var points = new Vector3[7];
                for (int c = 0; c <= 6; c++)
                { float angle = c * Mathf.PI / 3; points[c] = p + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * radius * 0.94f; }
                if (authoredWall >= 0)
                {
                    var response = HexSpellSceneSetup.ResolveProfile(spell.walls[authoredWall].response);
                    color = HexWallVisual.ColorFor(response != null ? response.mode : HexSpellEdgeMode.Fixed) * 0.55f; color.a = 1;
                }
                Handles.color = color; Handles.DrawAAConvexPolygon(points);
                Handles.color = offset == selected ? Color.white : new Color(0.3f, 0.35f, 0.4f);
                Handles.DrawAAPolyLine(offset == selected ? 3 : 1, points);
                if (allowed.Contains(offset)) for (int d = 0; d < 6; d++)
                    if (!allowed.Contains(offset.GetNeighbor(d)))
                    {
                        HexBoundarySettings response = spell.Boundary;
                        int corner = (7 - ((d + direction) % 6)) % 6;
                        Handles.color = BoundaryColor(response.mode); Handles.DrawAAPolyLine(3, points[corner], points[(corner + 1) % 6]);
                    }
                if (radius > 10)
                {
                    string label = cell == HexCoordinate.Origin ? "000" : authoredWall >= 0 ?
                        (spell.walls[authoredWall].response == Key_HexBoundaryProfilePP.Amplify ? "+" :
                        spell.walls[authoredWall].response == Key_HexBoundaryProfilePP.Absorb ? "-" : "W") : hit ? EventLabel(offset) : "";
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
            if (current.type == EventType.MouseDown && current.button == 0) stroke.Clear();
            if (current.type == EventType.MouseUp && current.button == 0) stroke.Clear();
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && rect.Contains(current.mousePosition))
            {
                Vector2 p = editView.ToLocal(current.mousePosition, rect, scale);
                var cell = HexCoordinate.FromLocalPosition(new Vector3(p.x, 0, -p.y), 1);
                if (cell.A >= min && cell.A < min + gridSize && cell.B >= min && cell.B < min + gridSize)
                {
                    selected = cell.Rotate(-direction);
                    if (stroke.Add(cell))
                    {
                        if (brush == Brush.Region)
                        {
                            Undo.RecordObject(spell, "Toggle region");
                            if (!spell.region.Remove(selected))
                            { spell.walls.RemoveAll(x => x.offset == selected); spell.region.Add(selected); }
                            else spell.impulses.RemoveAll(x => x.offset == selected);
                            Changed();
                        }
                        else if (brush == Brush.Event) AddEvent(selected);
                        else if (brush == Brush.SpellWall && selected != HexCoordinate.Origin && wallBrush != Key_HexBoundaryProfilePP.None)
                        {
                            Undo.RecordObject(spell, "Toggle spell wall");
                            if (spell.walls.RemoveAll(x => x.offset == selected) == 0)
                            {
                                spell.region.Remove(selected); spell.impulses.RemoveAll(x => x.offset == selected);
                                spell.walls.Add(new HexSpellWall { offset = selected, response = wallBrush });
                            }
                            Changed();
                        }
                        else if (brush == Brush.TestWall && paintBoundary.IsValid)
                        { if (!walls.Remove(cell)) walls.Add(cell, paintBoundary); ResetPreview(); }
                        else if (brush == Brush.Enemy)
                        {
                            var target = targets.Find(x => x.coordinate == cell);
                            if (target == null && Movable(cell)) targets.Add(new HexTrainingTarget { coordinate = cell });
                            else if (target != null) targets.Remove(target);
                            ResetPreview();
                        }
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
            Undo.RecordObject(spell, "Add spell event");
            spell.walls.RemoveAll(x => x.offset == offset);
            if (!spell.region.Contains(offset)) spell.region.Add(offset);
            spell.impulses.Add(new HexSpellImpulse(offset, 0, 8)); Changed();
        }
        private void Changed() { EditorUtility.SetDirty(spell); ResetPreview(); }
        private void ResetPreview()
        {
            world = null; previewCast = null; wallSources.Clear(); elapsed = accumulator = 0;
            nextDamageTime = Mathf.Max(0.01f, Damage.interval); playing = false;
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
            {
                var response = walls.TryGetValue(cell, out var value) ? value : HexBoundarySettings.Fixed;
                var profile = HexSpellSceneSetup.ResolveProfile(response.profile);
                source = new HexBoundarySource(response, true, profile != null ? profile.amplification : HexAmplifySettings.Default);
                wallSources.Add(cell, source);
            }
            return source;
        }
        private float Height(HexCoordinate cell) => world?.Height(cell) ?? 0;
        private bool StartPreview()
        {
            if (world == null) world = new HexWaveWorld(Movable, MapBoundary, HexSpellSceneSetup.ResolveProfile);
            previewCast = world.BeginCast(spell, HexCoordinate.Origin, direction, Settings, 1, true);
            return previewCast != null;
        }
        private void Advance()
        {
            if (world == null) return;
            world.Step(StepTime); elapsed = world.Time;
            foreach (var target in targets) target.peakHeight = Mathf.Max(target.peakHeight, Mathf.Abs(Height(target.coordinate)));
            while (elapsed + 1e-8 >= nextDamageTime)
            {
                foreach (var target in targets) if (Movable(target.coordinate)) target.Sample(Height(target.coordinate), Damage, nextDamageTime);
                nextDamageTime += Mathf.Max(0.01f, Damage.interval);
            }
            if (world.IsIdle)
            {
                if (loop) { ResetPreview(); playing = StartPreview(); }
                else playing = false;
            }
        }
        private void DrawPlayback()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
                using (new EditorGUI.DisabledScope(locked))
                {
                    if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton))
                    {
                        if (playing) playing = false;
                        else { if (world == null || world.IsIdle) { ResetPreview(); StartPreview(); } playing = world != null && !world.IsIdle; lastUpdate = EditorApplication.timeSinceStartup; }
                    }
                    if (GUILayout.Button("Replay", EditorStyles.toolbarButton)) { ResetPreview(); playing = StartPreview(); }
                    if (GUILayout.Button("Reset Simulation", EditorStyles.toolbarButton)) ResetPreview();
                    if (GUILayout.Button("Step", EditorStyles.toolbarButton))
                    { playing = false; if (world == null || world.IsIdle) { ResetPreview(); StartPreview(); } Advance(); }
                }
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton);
                playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.1f, 3, GUILayout.Width(120));
                GUILayout.Label($"{elapsed:0.00}s  {previewCast?.Phase.ToString() ?? "Stopped"}", GUILayout.Width(180));
            }
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && !EditorApplication.isPlayingOrWillChangePlaymode)
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
            if (world != null) foreach (var wave in world.Casts) for (int i = 0; i < wave.CellCount; i++)
                bounds.Encapsulate(wave.GetCoordinate(i).ToLocalPosition(1, wave.GetHeight(i) + 1));
            if (spell != null) foreach (var hit in spell.impulses)
                if (hit.mode == HexSpellEventMode.LiftAndRelease)
                    bounds.Encapsulate(hit.offset.Rotate(direction).ToLocalPosition(1, spell.LiftHeight(hit, 1) + 1));
            if (spell != null) foreach (var wall in spell.walls) bounds.Encapsulate(wall.offset.Rotate(direction).ToLocalPosition(1));
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
            try { Render3D(rect); renderError = null; }
            catch (Exception e)
            {
                DisposePreview(); renderError = "Preview resources were reset. " + e.Message;
            }
        }
        private void Render3D(Rect rect)
        {
            if (rect.width < 1 || rect.height < 1) return;
            if (preview == null || preview.camera == null ||
                preview.camera.targetTexture != null && !preview.camera.targetTexture.IsCreated())
            { DisposePreview(); preview = new PreviewRenderUtility(); }
            preview.camera.fieldOfView = 35;
            if (mesh == null) mesh = BuildColumn();
            if (dummyMesh == null) dummyMesh = BuildDummy();
            if (material == null)
            {
                var shader = Shader.Find("NangClicker/Hex Spell Preview");
                if (shader == null) throw new InvalidOperationException("Preview shader is not available.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (block == null) block = new MaterialPropertyBlock();
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
                if (world != null && world.Control(cell) != HexMotionControl.Free) color = new Color(0.9f, 0.45f, 1);
                if (walls.TryGetValue(cell, out var response)) color = HexWallVisual.ColorFor(response.mode);
                bool activeWall = world != null && world.IsTemporaryWall(cell);
                int wallIndex = spell.walls.FindIndex(x => x.offset == offset);
                if (activeWall) color = Color.Lerp(HexWallVisual.ColorFor(world.GetWallSource(cell).Settings.mode), Color.white, world.WallFlash(cell));
                else if (wallIndex >= 0)
                {
                    var profile = HexSpellSceneSetup.ResolveProfile(spell.walls[wallIndex].response);
                    color = Color.Lerp(color, HexWallVisual.ColorFor(profile != null ? profile.mode : HexSpellEdgeMode.Fixed), 0.35f);
                }
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
