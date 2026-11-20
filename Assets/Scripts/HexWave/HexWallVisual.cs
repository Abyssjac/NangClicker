using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NangClicker.HexWave
{
    /// <summary>Wall symbols, lifetime fade and directional contact flashes; contains no simulation state.</summary>
    [RequireComponent(typeof(HexWaveManager)), DisallowMultipleComponent]
    public sealed class HexWallVisual : MonoBehaviour
    {
        private sealed class Marker { public GameObject root; public LineRenderer outline; public LineRenderer[] edges; public TextMesh symbol; }
        private readonly Dictionary<HexCoordinate, Marker> markers = new Dictionary<HexCoordinate, Marker>();
        private HexWaveManager manager;
        private Material material;
        public static Color ColorFor(HexSpellEdgeMode mode) => mode == HexSpellEdgeMode.Amplify ? new Color(0.85f, 0.15f, 1) :
            mode == HexSpellEdgeMode.Absorb ? new Color(0.15f, 0.55f, 0.65f) : new Color(0.28f, 0.32f, 0.38f);
        private void Awake()
        {
            manager = GetComponent<HexWaveManager>();
            var shader = Shader.Find("NangClicker/Hex Spell Overlay");
            if (shader != null) material = new Material(shader) { name = "Runtime Wall Marks" };
        }
        private void LateUpdate()
        {
            if (manager == null || material == null || manager.World == null) return;
            foreach (var marker in markers.Values) marker.root.SetActive(false);
            for (int i = 0; i < manager.CellCount; i++)
            {
                var cell = manager.GetCellView(i);
                if (!manager.IsWaveWall(cell.Coordinate)) continue;
                var mode = manager.GetWallResponseMode(cell.Coordinate);
                if (mode == HexSpellEdgeMode.Fixed) continue;
                if (!markers.TryGetValue(cell.Coordinate, out var mark))
                {
                    mark = CreateMarker(); markers.Add(cell.Coordinate, mark);
                }
                mark.root.SetActive(true);
                mark.root.transform.position = cell.transform.position;
                mark.root.transform.rotation = manager.CellsRoot.rotation;
                mark.root.transform.localScale = Vector3.one * manager.Spacing;
                float fade = Mathf.Lerp(0.3f, 1, Mathf.Clamp01(manager.World.WallRemaining01(cell.Coordinate) * 4));
                var color = ColorFor(mode); color.a = fade;
                mark.outline.startColor = mark.outline.endColor = color;
                mark.symbol.text = mode == HexSpellEdgeMode.Amplify ? "+" : "-";
                mark.symbol.color = Color.Lerp(color, Color.white, manager.World.WallFlash(cell.Coordinate));
                if (Camera.main != null) mark.symbol.transform.rotation = Camera.main.transform.rotation;
                for (int d = 0; d < 6; d++)
                {
                    float flash = manager.World.ContactFlash(cell.Coordinate, cell.Coordinate.GetNeighbor(d));
                    var edgeColor = color; edgeColor.a = fade * flash;
                    mark.edges[d].startColor = mark.edges[d].endColor = edgeColor;
                }
            }
        }
        private Marker CreateMarker()
        {
            var mark = new Marker { root = new GameObject("Wave Wall Indicator") };
            mark.root.transform.SetParent(transform, false);
            mark.outline = Line("Outline", mark.root.transform, 0.04f); mark.outline.positionCount = 7;
            for (int c = 0; c <= 6; c++) mark.outline.SetPosition(c, Corner(c));
            mark.edges = new LineRenderer[6];
            for (int d = 0; d < 6; d++)
            {
                var edge = mark.edges[d] = Line("Contact " + d, mark.root.transform, 0.09f); edge.positionCount = 2;
                int corner = (7 - d) % 6; edge.SetPosition(0, Corner(corner)); edge.SetPosition(1, Corner(corner + 1));
            }
            var label = new GameObject("Response"); label.transform.SetParent(mark.root.transform, false);
            label.transform.localPosition = Vector3.up * 1.7f;
            mark.symbol = label.AddComponent<TextMesh>(); mark.symbol.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mark.symbol.fontSize = 40; mark.symbol.characterSize = 0.075f; mark.symbol.anchor = TextAnchor.MiddleCenter;
            label.GetComponent<Renderer>().sharedMaterial = mark.symbol.font.material;
            return mark;
        }
        private LineRenderer Line(string name, Transform parent, float width)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false;
            line.widthMultiplier = width; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        private static Vector3 Corner(int c)
        { float angle = c * Mathf.PI / 3; return new Vector3(Mathf.Cos(angle) / Mathf.Sqrt(3), 1.55f, Mathf.Sin(angle) / Mathf.Sqrt(3)); }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
