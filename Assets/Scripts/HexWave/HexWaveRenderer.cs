using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>Publishes one interpolated terrain pose before character movement. Materials own their base colors.</summary>
    [DefaultExecutionOrder(-90), DisallowMultipleComponent]
    public sealed class HexWaveRenderer : MonoBehaviour
    {
        [SerializeField] private HexWaveManager manager;
        [SerializeField] private bool interpolatePhysicsSnapshots = true;
        // Retained serialized fields preserve authored legacy settings; the terrain material path does not write their colors.
        [SerializeField, HideInInspector] private float colorHeightRange;
        [SerializeField, HideInInspector] private Gradient heightGradient;
        [SerializeField, HideInInspector] private Color wallColor;
        [SerializeField, HideInInspector] private string colorPropertyName;
        private void Reset() => manager = GetComponent<HexWaveManager>();
        private void Awake()
        {
            if (manager == null) manager = GetComponent<HexWaveManager>();
            ApplyMaterials();
        }
        private void Update() => RefreshPhysicsPose();
        public void RefreshPhysicsPose()
        {
            if (manager == null || !manager.IsInitialized) return;
            float interpolation = interpolatePhysicsSnapshots && Time.fixedDeltaTime > Mathf.Epsilon
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1;
            bool moved = false;
            for (int i = 0; i < manager.CellCount; i++)
            {
                var cell = manager.GetCellView(i); if (cell == null) continue;
                float h = Mathf.Lerp(manager.GetPreviousHeight(i), manager.GetCurrentHeight(i), interpolation);
                var before = cell.transform.localPosition; cell.ApplyHeight(h);
                moved |= before != cell.transform.localPosition;
            }
            if (moved) Physics.SyncTransforms();
        }
        private void ApplyMaterials()
        {
            if (manager == null) return;
            for (int i = 0; i < manager.CellCount; i++)
            {
                var cell = manager.GetCellView(i); if (cell == null) continue;
                var surface = manager.GetSurfaceProfile(cell.Surface);
                cell.SetSurface(cell.Surface, surface != null ? surface.material : null);
                if (cell.TargetRenderer != null) cell.TargetRenderer.SetPropertyBlock(null);
            }
        }
        public void Configure(HexWaveManager targetManager, float unusedLegacyRange)
        { manager = targetManager; ApplyMaterials(); }
        public void RefreshBoundaryPreview()
        {
            if (manager == null) manager = GetComponent<HexWaveManager>();
            ApplyMaterials();
        }
    }
}
