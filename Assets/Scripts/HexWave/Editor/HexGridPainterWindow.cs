using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NangClicker.HexWave.Editor
{
    public sealed class HexGridPainterWindow : EditorWindow
    {
        private enum Layer { Structure, Surface }
        private enum StructureBrush { Select, Floor, Wall, Erase }
        [SerializeField] private HexWaveManager manager;
        [SerializeField] private Layer layer;
        [SerializeField] private StructureBrush structureBrush = StructureBrush.Floor;
        [SerializeField] private Key_HexBoundaryProfilePP wallResponse = Key_HexBoundaryProfilePP.Fixed;
        [SerializeField] private Key_HexTerrainSurfacePP surface = Key_HexTerrainSurfacePP.Grass;
        [SerializeField] private float wallHeight, wallDepth = 1.5f;
        [SerializeField] private int brushRadius;
        [SerializeField] private HexEditorCanvasView canvas = new HexEditorCanvasView();
        [SerializeField] private bool paintInScene = true;
        private readonly Dictionary<HexCoordinate, HexCellView> cells = new Dictionary<HexCoordinate, HexCellView>();
        private readonly HashSet<HexCoordinate> stroke = new HashSet<HexCoordinate>();
        private HexCoordinate selected;
        private bool painting, geometryChanged, changed;
        private int undoGroup;
        private Rect mapRect;
        private Vector2 inspectorScroll;
        private const float BaseScale = 24;

        [MenuItem("Tools/Hex Wave/Grid Painter")]
        public static void OpenFromMenu() => Open(FindFirstObjectByType<HexWaveManager>());
        public static void Open(HexWaveManager targetManager)
        {
            var window = GetWindow<HexGridPainterWindow>("Hex Map Editor");
            window.manager = targetManager; window.minSize = new Vector2(1000, 650);
            if (targetManager != null && !Application.isPlaying) targetManager.RebuildGrid();
            window.RefreshCache(); window.Show(); window.FrameMap();
        }
        private void OnEnable()
        {
            if (canvas == null) canvas = new HexEditorCanvasView();
            SceneView.duringSceneGui += OnSceneGUI; Undo.undoRedoPerformed += UndoRedo;
            RefreshCache();
        }
        private void OnDisable() { EndStroke(); SceneView.duringSceneGui -= OnSceneGUI; Undo.undoRedoPerformed -= UndoRedo; }
        private void OnLostFocus() => EndStroke();
        private void UndoRedo()
        {
            RefreshCache(); if (manager != null && !Application.isPlaying) manager.RebuildGrid();
            Repaint(); SceneView.RepaintAll();
        }
        private void RefreshCache()
        {
            cells.Clear();
            if (manager == null) return;
            foreach (var cell in manager.CellsRoot.GetComponentsInChildren<HexCellView>(true)) cells[cell.Coordinate] = cell;
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var next = (HexWaveManager)EditorGUILayout.ObjectField(manager, typeof(HexWaveManager), true, GUILayout.Width(260));
                if (EditorGUI.EndChangeCheck()) { EndStroke(); manager = next; RefreshCache(); FrameMap(); }
                layer = (Layer)GUILayout.Toolbar((int)layer, new[] { "Structure", "Surface / Material" }, GUILayout.Width(330));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Frame Map", EditorStyles.toolbarButton)) FrameMap();
                if (GUILayout.Button("Reset View", EditorStyles.toolbarButton)) canvas.Reset();
                if (GUILayout.Button("Save Scene", EditorStyles.toolbarButton) && manager != null && !Application.isPlaying)
                { EndStroke(); EditorSceneManager.SaveScene(manager.gameObject.scene); AssetDatabase.SaveAssets(); }
            }
            if (manager == null) { EditorGUILayout.HelpBox("Select a HexWaveManager in the scene.", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(270))) DrawInspector();
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Wheel: zoom | MMB: pan | LMB: paint | one undo per stroke", EditorStyles.miniLabel);
                    mapRect = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawCanvas(mapRect);
                }
            }
        }
        private void DrawInspector()
        {
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.IntSlider("Radius (hex steps)", brushRadius, 0, 5);
            if (layer == Layer.Structure)
            {
                structureBrush = (StructureBrush)GUILayout.Toolbar((int)structureBrush, new[] { "Select", "Floor", "Wall", "Erase" });
                if (structureBrush == StructureBrush.Wall)
                {
                    wallResponse = (Key_HexBoundaryProfilePP)EditorGUILayout.EnumPopup("Wall response", wallResponse);
                    wallHeight = EditorGUILayout.FloatField("Top offset", wallHeight);
                    wallDepth = Mathf.Max(0.05f, EditorGUILayout.FloatField("Column depth", wallDepth));
                    EditorGUILayout.HelpBox("Top offset is relative to the floor. Depth extends downward without changing the top. Fixed / Absorb / Amplify only.", MessageType.None);
                }
            }
            else
            {
                foreach (var key in new[] { Key_HexTerrainSurfacePP.Grass, Key_HexTerrainSurfacePP.Dirt, Key_HexTerrainSurfacePP.Rock, Key_HexTerrainSurfacePP.Snow, Key_HexTerrainSurfacePP.Water })
                {
                    var profile = HexTerrainAuthoring.Resolve(key);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var rect = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22));
                        EditorGUI.DrawRect(rect, profile != null ? profile.editorColor : Color.gray);
                        if (GUILayout.Toggle(surface == key, profile != null ? profile.displayName : key.ToString(), "Button")) surface = key;
                    }
                }
                EditorGUILayout.HelpBox("Paints existing cells only. Geometry, wall response and navigation are unchanged.", MessageType.None);
            }
            paintInScene = EditorGUILayout.Toggle("Enable Scene brush", paintInScene);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Selected " + selected, EditorStyles.boldLabel);
            if (cells.TryGetValue(selected, out var cell) && cell != null)
            {
                EditorGUILayout.LabelField("Role", cell.IsWall ? "Wall" : "Floor");
                EditorGUILayout.LabelField("Surface", cell.Surface.ToString());
                EditorGUILayout.LabelField("Top / depth", cell.WallTopHeight.ToString("0.###") + " / " + cell.ColumnDepth.ToString("0.###"));
                if (GUILayout.Button("Select Scene Object")) Selection.activeGameObject = cell.gameObject;
                if (GUILayout.Button("Apply current brush to selected"))
                { BeginStroke(); Stamp(selected); EndStroke(); }
            }
            else EditorGUILayout.LabelField("Empty cell");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cells", cells.Count.ToString());
            EditorGUILayout.LabelField("Spacing", manager.Spacing.ToString("0.###"));
            EditorGUILayout.ObjectField("Cell prefab", manager.CellPrefab, typeof(HexCellView), false);
            if (GUILayout.Button("Rebuild Topology"))
            { EndStroke(); manager.RebuildGrid(); HexWaveEditorVisuals.RefreshBoundaryPreview(manager); SceneView.RepaintAll(); }
            EditorGUILayout.EndScrollView();
        }
        private static Vector2 Plane(HexCoordinate c, float spacing)
        { var p = c.ToLocalPosition(spacing); return new Vector2(p.x, -p.z); }
        private HexCoordinate FromScreen(Vector2 p, Rect rect)
        {
            var local = canvas.ToLocal(p, rect, BaseScale);
            return HexCoordinate.FromLocalPosition(new Vector3(local.x, 0, -local.y), manager.Spacing);
        }
        private void DrawCanvas(Rect rect)
        {
            var e = Event.current; int control = GUIUtility.GetControlID(84113, FocusType.Passive);
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            { canvas.ZoomAt(e.mousePosition, rect, BaseScale, e.delta.y); e.Use(); Repaint(); }
            if (e.type == EventType.MouseDown && e.button == 2 && rect.Contains(e.mousePosition)) { GUIUtility.hotControl = control; e.Use(); }
            else if (e.type == EventType.MouseDrag && e.button == 2 && GUIUtility.hotControl == control)
            { canvas.pan += e.delta; e.Use(); Repaint(); }
            else if (e.type == EventType.MouseUp && e.button == 2 && GUIUtility.hotControl == control) { GUIUtility.hotControl = 0; e.Use(); }
            EditorGUI.DrawRect(rect, new Color(0.065f, 0.08f, 0.105f));
            GUI.BeginClip(rect); Handles.BeginGUI();
            float radius = (manager.CellPrefab != null ? manager.CellPrefab.ColumnRadius : 0.6f) * BaseScale * canvas.zoom;
            var corners = new[] { FromScreen(rect.position, rect), FromScreen(new Vector2(rect.xMax, rect.y), rect),
                FromScreen(new Vector2(rect.x,rect.yMax),rect), FromScreen(new Vector2(rect.xMax,rect.yMax),rect) };
            int minA = int.MaxValue, maxA = int.MinValue, minB = int.MaxValue, maxB = int.MinValue;
            foreach (var c in corners) { minA=Mathf.Min(minA,c.A); maxA=Mathf.Max(maxA,c.A); minB=Mathf.Min(minB,c.B); maxB=Mathf.Max(maxB,c.B); }
            maxA = Mathf.Min(maxA + 2, minA + 90); maxB = Mathf.Min(maxB + 2,minB + 90);
            for (int a=minA-2;a<=maxA;a++) for(int b=minB-2;b<=maxB;b++)
            {
                var c=HexCoordinate.FromAxial(a,b); var p=canvas.ToScreen(Plane(c,manager.Spacing),rect,BaseScale)-rect.position;
                if(p.x < -radius || p.x > rect.width+radius || p.y < -radius || p.y > rect.height+radius) continue;
                bool exists=cells.TryGetValue(c,out var cell) && cell!=null;
                var profile=exists?HexTerrainAuthoring.Resolve(cell.Surface):null;
                Color fill=exists?(profile!=null?profile.editorColor:new Color(0.25f,0.35f,0.28f)):new Color(0.095f,0.11f,0.14f);
                var points=HexPoints(p,radius*0.97f);
                Handles.color=fill; Handles.DrawAAConvexPolygon(points);
                Handles.color=c==selected?Color.white:exists&&cell.IsFixedBoundary?HexWallVisual.ColorFor(cell.WallResponse.mode):new Color(0.18f,0.21f,0.24f);
                Handles.DrawAAPolyLine(c==selected?3:1,points);
                if(exists&&cell.IsWall&&radius>13)
                {
                    string label=cell.WallResponse.mode==HexSpellEdgeMode.Amplify?"+":cell.WallResponse.mode==HexSpellEdgeMode.Absorb?"-":"#";
                    GUI.Label(new Rect(p.x-9,p.y-10,50,22),label,EditorStyles.whiteLabel);
                }
            }
            Handles.EndGUI(); GUI.EndClip();
            if (!Application.isPlaying && e.type == EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition))
            { GUIUtility.hotControl=control; BeginStroke(); PaintArea(FromScreen(e.mousePosition,rect)); e.Use(); }
            else if(e.type==EventType.MouseDrag && e.button==0 && painting && GUIUtility.hotControl==control)
            { PaintArea(FromScreen(e.mousePosition,rect)); e.Use(); }
            else if(e.type==EventType.MouseUp && e.button==0 && painting)
            { EndStroke(); if(GUIUtility.hotControl==control) GUIUtility.hotControl=0; e.Use(); }
            if (painting && e.rawType == EventType.MouseUp && e.button == 0) EndStroke();
        }
        private static Vector3[] HexPoints(Vector2 p,float radius)
        {
            var points=new Vector3[7];
            for(int i=0;i<=6;i++){float a=i*Mathf.PI/3; points[i]=p+new Vector2(Mathf.Cos(a),-Mathf.Sin(a))*radius;}
            return points;
        }
        private void BeginStroke()
        {
            if(painting)return; painting=true; stroke.Clear(); geometryChanged=changed=false;
            Undo.IncrementCurrentGroup(); undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Paint Hex "+layer);
        }
        private void PaintArea(HexCoordinate center)
        {
            selected=center;
            for(int a=-brushRadius;a<=brushRadius;a++) for(int b=-brushRadius;b<=brushRadius;b++)
            {var offset=HexCoordinate.FromAxial(a,b); if(offset.DistanceTo(HexCoordinate.Origin)<=brushRadius) Stamp(center+offset);}
            Repaint(); SceneView.RepaintAll();
        }
        private void Stamp(HexCoordinate c)
        {
            if (Application.isPlaying) return;
            if(!stroke.Add(c)||manager==null)return;
            bool exists=cells.TryGetValue(c,out var cell)&&cell!=null;
            if(layer==Layer.Surface)
            {
                var profile=HexTerrainAuthoring.Resolve(surface); if(!exists||profile==null)return;
                HexTerrainAuthoring.RecordCell(cell,"Paint Hex Surface"); cell.SetSurface(surface,profile.material);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cell);
                if (cell.TargetRenderer != null) PrefabUtility.RecordPrefabInstancePropertyModifications(cell.TargetRenderer);
                EditorUtility.SetDirty(cell); changed=true; return;
            }
            if(structureBrush==StructureBrush.Select)return;
            if(structureBrush==StructureBrush.Erase)
            {if(exists){Undo.DestroyObjectImmediate(cell.gameObject);cells.Remove(c);geometryChanged=changed=true;} return;}
            if(structureBrush==StructureBrush.Wall && HexSpellSceneSetup.ResolveProfile(wallResponse)==null)return;
            if(!exists)
            {
                if(manager.CellPrefab==null)return;
                var root=(GameObject)PrefabUtility.InstantiatePrefab(manager.CellPrefab.gameObject,manager.CellsRoot);
                Undo.RegisterCreatedObjectUndo(root,"Paint Hex Cell"); cell=root.GetComponent<HexCellView>();
                root.name=$"Hex {c.A}_{c.B}_{c.C}"; root.transform.localPosition=c.ToLocalPosition(manager.Spacing);
                root.transform.localRotation=Quaternion.identity; cell.SetCoordinate(c);
                var key=structureBrush==StructureBrush.Wall?Key_HexTerrainSurfacePP.Rock:Key_HexTerrainSurfacePP.Grass;
                var profile=HexTerrainAuthoring.Resolve(key); if(profile!=null) cell.SetSurface(key,profile.material);
                cells[c]=cell;
            }
            HexTerrainAuthoring.RecordCell(cell,"Paint Hex Structure");
            if(structureBrush==StructureBrush.Wall)
            {
                var profile=HexSpellSceneSetup.ResolveProfile(wallResponse); if(profile==null)return;
                cell.SetWallResponse(profile.Settings); cell.SetWallShape(wallHeight,wallDepth);
            }
            else cell.SetWall(false);
            HexTerrainAuthoring.Apply(cell); geometryChanged=changed=true;
        }
        private void EndStroke()
        {
            if(!painting)return; painting=false;
            if(manager!=null&&changed)
            {
                if(geometryChanged){manager.RebuildGrid();HexWaveEditorVisuals.RefreshBoundaryPreview(manager);}
                EditorSceneManager.MarkSceneDirty(manager.gameObject.scene); SceneView.RepaintAll();
            }
            Undo.CollapseUndoOperations(undoGroup); stroke.Clear(); changed=geometryChanged=false;
        }
        private void FrameMap()
        {
            if(canvas==null)canvas=new HexEditorCanvasView();
            if(manager==null||cells.Count==0){canvas.Reset();return;}
            var rect=mapRect.width>1?mapRect:new Rect(270,60,Mathf.Max(400,position.width-270),Mathf.Max(300,position.height-100));
            bool first=true; Vector2 min=Vector2.zero,max=Vector2.zero;
            foreach(var pair in cells){var p=Plane(pair.Key,manager.Spacing);if(first){min=max=p;first=false;}else{min=Vector2.Min(min,p);max=Vector2.Max(max,p);}}
            var size=max-min+Vector2.one*manager.Spacing*3;
            canvas.zoom=Mathf.Clamp(Mathf.Min(rect.width/size.x,rect.height/size.y)/BaseScale,0.12f,12);
            canvas.pan=-(min+max)*0.5f*(BaseScale*canvas.zoom);Repaint();
        }
        private void OnSceneGUI(SceneView view)
        {
            if(manager==null||Application.isPlaying||!paintInScene)return;
            var e=Event.current;if(e.alt)return;
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            HexCoordinate c;
            if(manager.TryRaycastCell(ray,out var cell,out _)) c=cell.Coordinate;
            else
            {
                var plane=new Plane(manager.CellsRoot.up,manager.CellsRoot.TransformPoint(Vector3.up*HexColumnMeshBuilder.TopDatum));
                if(!plane.Raycast(ray,out float t))return;
                c=HexCoordinate.FromLocalPosition(manager.CellsRoot.InverseTransformPoint(ray.GetPoint(t)),manager.Spacing);
            }
            Vector3 center=cells.TryGetValue(c,out var existing)&&existing!=null?existing.SurfacePoint:
                manager.CellsRoot.TransformPoint(c.ToLocalPosition(manager.Spacing,HexColumnMeshBuilder.TopDatum));
            var points=new Vector3[7];float radius=manager.CellPrefab!=null?manager.CellPrefab.ColumnRadius:0.6f;
            for(int i=0;i<=6;i++){float a=i*Mathf.PI/3;points[i]=center+manager.CellsRoot.TransformVector(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}
            Handles.color=layer==Layer.Surface?new Color(0.3f,0.8f,1):Color.yellow;Handles.DrawAAPolyLine(3,points);
            if(layer==Layer.Structure&&structureBrush==StructureBrush.Select)return;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(e.type==EventType.MouseDown&&e.button==0){BeginStroke();PaintArea(c);e.Use();}
            else if(e.type==EventType.MouseDrag&&e.button==0&&painting){PaintArea(c);e.Use();}
            else if(e.type==EventType.MouseUp&&e.button==0&&painting){EndStroke();e.Use();}
        }
    }
}
