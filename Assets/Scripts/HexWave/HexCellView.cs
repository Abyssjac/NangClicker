using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexCellView : MonoBehaviour
    {
        [SerializeField] private HexCoordinate coordinate;
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("An authored map wall. Separate from automatic outer boundaries and spell masks.")]
        [SerializeField] private bool isWall;
        [SerializeField] private bool useWallResponse;
        [SerializeField] private HexBoundarySettings wallResponse = HexBoundarySettings.Fixed;
        [Header("Terrain authoring")]
        [SerializeField] private Key_HexTerrainSurfacePP surface = Key_HexTerrainSurfacePP.Grass;
        [Min(0.01f), SerializeField] private float columnRadius = 0.6f;
        [Min(0.05f), SerializeField] private float wallDepth = 1.5f;
        [SerializeField] private float wallTopHeight;
        [SerializeField, HideInInspector] private int terrainSchemaVersion;
        [SerializeField, HideInInspector] private Vector3 baseLocalPosition;
        [SerializeField, HideInInspector] private bool fixedBoundary;

        public Key_HexTerrainSurfacePP Surface => surface;
        public int TerrainSchemaVersion => terrainSchemaVersion;
        public void MarkTerrainAuthored() { terrainSchemaVersion = 1; }
        public float ColumnRadius => columnRadius;
        public float ColumnDepth => isWall ? wallDepth : HexColumnMeshBuilder.TopDatum;
        public float WallTopHeight => wallTopHeight;
        public Vector3 SurfacePoint => transform.TransformPoint(Vector3.up * HexColumnMeshBuilder.TopDatum);
        public Collider SurfaceCollider => GetComponent<Collider>();
        public HexCoordinate Coordinate => coordinate;
        public Renderer TargetRenderer => targetRenderer;
        public Vector3 BaseLocalPosition => baseLocalPosition;
        public bool IsFixedBoundary => fixedBoundary;
        public bool IsWall => isWall;
        public void SetWall(bool value) { isWall = value; }
        public void SetSurface(Key_HexTerrainSurfacePP key, Material material)
        {
            surface = key;
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null && material != null)
            {
                targetRenderer.sharedMaterial = material;
                targetRenderer.SetPropertyBlock(null);
            }
        }
        public void SetWallShape(float height, float depth)
        { wallTopHeight = height; wallDepth = Mathf.Max(0.05f, depth); }
        public void SetRadius(float radius) { columnRadius = Mathf.Max(0.01f, radius); }
        public void ApplyAuthoringGeometry(Mesh mesh)
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;
            var collider = GetComponent<MeshCollider>();
            if (collider != null) collider.sharedMesh = mesh;
            var p = transform.localPosition; p.y = isWall ? wallTopHeight : 0;
            transform.localPosition = p; CaptureBaseLocalPosition();
        }
        public HexBoundarySettings WallResponse => useWallResponse && wallResponse.IsValid ? wallResponse : HexBoundarySettings.Fixed;
        public void SetWallResponse(HexBoundarySettings response)
        { isWall = true; useWallResponse = true; wallResponse = response; }
        public int RuntimeIndex { get; private set; } = -1;

        private void Reset()
        {
            targetRenderer = GetComponentInChildren<Renderer>();
            baseLocalPosition = transform.localPosition;
        }

        private void OnValidate()
        {
            columnRadius = Mathf.Max(0.01f, columnRadius); wallDepth = Mathf.Max(0.05f, wallDepth);
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
        }

        public void SetCoordinate(HexCoordinate value)
        {
            coordinate = value;
        }

        public void CaptureBaseLocalPosition()
        {
            baseLocalPosition = transform.localPosition;
        }

        public void SetRuntimeBinding(int index, bool isFixedBoundary)
        {
            RuntimeIndex = index;
            fixedBoundary = isFixedBoundary;
        }

        public void ApplyHeight(float height)
        {
            transform.localPosition = baseLocalPosition + Vector3.up * height;
        }

        public void ApplyColor(MaterialPropertyBlock propertyBlock, int colorPropertyId, Color color)
        {
            if (targetRenderer == null)
                return;

            propertyBlock.Clear();
            propertyBlock.SetColor(colorPropertyId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
