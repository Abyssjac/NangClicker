using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexSpellPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private Material sphereFieldMaterial;
        [SerializeField] private bool showEventLabels = true;
        private sealed class Marker
        {
            public GameObject root;
            public Renderer fill;
            public LineRenderer outline;
            public LineRenderer[] edges;
            public TextMesh label;
        }
        private readonly List<Marker> markers = new List<Marker>();
        private MaterialPropertyBlock properties;
        private Mesh hexMesh;
        private GameObject anchor;
        private LineRenderer arrow;
        private readonly HashSet<HexCoordinate> mask = new HashSet<HexCoordinate>();

        public void Hide()
        { foreach (var marker in markers) marker.root.SetActive(false); if (anchor != null) anchor.SetActive(false); if (arrow != null) arrow.gameObject.SetActive(false); }

        public void Show(HexWaveManager map, HexSpellProperty spell, HexCoordinate origin, int direction, float multiplier)
        {
            if (overlayMaterial == null) return;
            EnsureResources();
            mask.Clear(); foreach (var offset in spell.region) mask.Add(origin + offset.Rotate(direction));
            int index = 0;
            bool validOrigin = map.IsMovable(origin);
            foreach (var coordinate in mask)
            {
                if (index == markers.Count) markers.Add(CreateMarker());
                Marker marker = markers[index++]; marker.root.SetActive(true);
                Vector3 center = GetTop(map, coordinate);
                marker.root.transform.position = center;
                marker.root.transform.rotation = map.CellsRoot.rotation;
                marker.root.transform.localScale = Vector3.one * map.Spacing;
                bool valid = validOrigin && map.IsMovable(coordinate);
                float total = 0f; int hits = 0; float firstTime = float.MaxValue;
                foreach (var hit in spell.impulses)
                    if (origin + hit.offset.Rotate(direction) == coordinate)
                    { total += hit.impulse * spell.strength * multiplier; hits++; firstTime = Mathf.Min(firstTime, hit.time); }
                Color color = !valid ? new Color(0.45f, 0.48f, 0.52f, 0.3f) : hits == 0
                    ? new Color(0.05f, 0.5f, 1f, 0.14f) : total >= 0f
                    ? new Color(1f, 0.08f, 0.04f, 0.82f) : new Color(0.75f, 0.18f, 1f, 0.82f);
                properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color); marker.fill.SetPropertyBlock(properties);
                for (int d = 0; d < 6; d++)
                {
                    bool boundary = !mask.Contains(coordinate.GetNeighbor(d));
                    marker.edges[d].gameObject.SetActive(boundary);
                    if (boundary)
                    {
                        int corner = (7 - d) % 6;
                        marker.edges[d].SetPosition(0, Corner(corner)); marker.edges[d].SetPosition(1, Corner(corner + 1));
                        marker.edges[d].startColor = marker.edges[d].endColor = valid ? new Color(0.1f, 0.65f, 1f) : Color.gray;
                    }
                }
                // A complete thin hex outline keeps clipped/invalid cells legible too.
                marker.outline.positionCount = 7;
                for (int c = 0; c <= 6; c++) marker.outline.SetPosition(c, Corner(c));
                marker.outline.startColor = marker.outline.endColor = valid ? new Color(0.1f, 0.65f, 1f, 0.85f) : Color.gray;
                marker.outline.widthMultiplier = 0.008f;
                marker.label.text = !valid ? "×" : showEventLabels && hits > 0 ? $"{total:+0.##;-0.##;0}\n{firstTime:0.##}s" : "";
                marker.label.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
                float visualScale = 0.065f + Mathf.Min(0.02f, Mathf.Abs(total) * 0.002f);
                marker.label.characterSize = visualScale;
            }
            for (int i = index; i < markers.Count; i++) markers[i].root.SetActive(false);
            anchor.SetActive(true); anchor.transform.position = GetTop(map, origin) + Vector3.up * 0.22f;
            anchor.transform.localScale = Vector3.one * (map.Spacing * 0.28f);
            arrow.gameObject.SetActive(true);
            Vector3 start = GetTop(map, origin) + Vector3.up * 0.55f;
            Vector3 forward = map.CellsRoot.TransformDirection(HexCoordinate.GetNeighborDirection(1).Rotate(direction).ToLocalPosition(1f)).normalized;
            Vector3 end = start + forward * map.Spacing;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * map.Spacing * 0.22f;
            arrow.positionCount = 5; arrow.SetPosition(0, start); arrow.SetPosition(1, end);
            arrow.SetPosition(2, end - forward * map.Spacing * 0.3f + side); arrow.SetPosition(3, end);
            arrow.SetPosition(4, end - forward * map.Spacing * 0.3f - side);
        }

        private static Vector3 Corner(int i)
        { float angle = i * Mathf.PI / 3f; return new Vector3(Mathf.Cos(angle), 0.02f, Mathf.Sin(angle)) / Mathf.Sqrt(3f); }
        private static Vector3 GetTop(HexWaveManager map, HexCoordinate coordinate)
        {
            if (map.TryGetCell(coordinate, out var cell))
            { Vector3 p = cell.transform.position; p.y = cell.TargetRenderer != null ? cell.TargetRenderer.bounds.max.y + 0.035f : p.y + 1.535f; return p; }
            return map.CellsRoot.TransformPoint(coordinate.ToLocalPosition(map.Spacing, 1.535f));
        }
        private void EnsureResources()
        {
            if (hexMesh != null) return;
            properties = new MaterialPropertyBlock();
            var vertices = new Vector3[7]; var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            { vertices[i + 1] = Corner(i); triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % 6 + 1; triangles[i * 3 + 2] = i + 1; }
            hexMesh = new Mesh { name = "Spell Preview Hex" }; hexMesh.vertices = vertices; hexMesh.triangles = triangles; hexMesh.RecalculateNormals();
            var colors = new Color[7]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white; hexMesh.colors = colors;
            anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere); anchor.name = "Spell Anchor"; anchor.transform.SetParent(transform, false);
            Destroy(anchor.GetComponent<Collider>());
            anchor.GetComponent<Renderer>().sharedMaterial = sphereFieldMaterial != null ? sphereFieldMaterial : overlayMaterial;
            arrow = MakeLine("Spell Direction", transform); arrow.useWorldSpace = true; arrow.widthMultiplier = 0.055f;
            arrow.startColor = arrow.endColor = Color.white;
        }
        private Marker CreateMarker()
        {
            var marker = new Marker(); marker.root = new GameObject("Spell Range Hex"); marker.root.transform.SetParent(transform, false);
            marker.root.AddComponent<MeshFilter>().sharedMesh = hexMesh;
            marker.fill = marker.root.AddComponent<MeshRenderer>(); marker.fill.sharedMaterial = overlayMaterial;
            marker.fill.shadowCastingMode = ShadowCastingMode.Off; marker.fill.receiveShadows = false;
            marker.outline = MakeLine("Boundary", marker.root.transform);
            marker.edges = new LineRenderer[6];
            for (int d = 0; d < 6; d++)
            { marker.edges[d] = MakeLine("Mask Edge " + d, marker.root.transform); marker.edges[d].positionCount = 2; marker.edges[d].widthMultiplier = 0.04f; }
            var label = new GameObject("Impulse Label"); label.transform.SetParent(marker.root.transform, false); label.transform.localPosition = Vector3.up * 0.5f;
            marker.label = label.AddComponent<TextMesh>(); marker.label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            marker.label.anchor = TextAnchor.MiddleCenter; marker.label.alignment = TextAlignment.Center; marker.label.fontSize = 32;
            label.GetComponent<Renderer>().sharedMaterial = marker.label.font.material;
            return marker;
        }
        private LineRenderer MakeLine(string name, Transform parent)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false); var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = overlayMaterial; line.useWorldSpace = false; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (hexMesh != null) Destroy(hexMesh); }
    }
}
