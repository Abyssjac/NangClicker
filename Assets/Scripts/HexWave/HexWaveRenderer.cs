using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexWaveRenderer : MonoBehaviour
    {
        [SerializeField] private HexWaveManager manager;
        [SerializeField] private bool interpolatePhysicsSnapshots = true;
        [Min(0.0001f)] [SerializeField] private float colorHeightRange = 1f;
        [SerializeField] private Gradient heightGradient = CreateDefaultGradient();
        [SerializeField] private Color wallColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        [SerializeField] private string colorPropertyName = "_BaseColor";

        private MaterialPropertyBlock propertyBlock;
        private int colorPropertyId;
        private int fallbackColorPropertyId;

        private void Reset()
        {
            manager = GetComponent<HexWaveManager>();
            heightGradient = CreateDefaultGradient();
        }

        private void Awake()
        {
            if (manager == null)
                manager = GetComponent<HexWaveManager>();

            EnsurePropertyBlock();
            RefreshPropertyIds();
        }

        private void OnEnable()
        {
            EnsurePropertyBlock();
        }

        private void OnValidate()
        {
            colorHeightRange = Mathf.Max(0.0001f, colorHeightRange);
            if (string.IsNullOrWhiteSpace(colorPropertyName))
                colorPropertyName = "_BaseColor";
            RefreshPropertyIds();
        }

        private void LateUpdate()
        {
            if (manager == null || !manager.IsInitialized)
                return;

            EnsurePropertyBlock();

            float interpolation = 1f;
            if (interpolatePhysicsSnapshots && Time.fixedDeltaTime > Mathf.Epsilon)
                interpolation = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

            for (int i = 0; i < manager.CellCount; i++)
            {
                HexCellView cell = manager.GetCellView(i);
                if (cell == null)
                    continue;

                if (manager.IsFixedBoundary(i))
                {
                    cell.ApplyHeight(0f);
                    int wallPropertyId = ResolveColorProperty(cell);
                    cell.ApplyColor(propertyBlock, wallPropertyId, wallColor);
                    continue;
                }

                float height = Mathf.Lerp(
                    manager.GetPreviousHeight(i),
                    manager.GetCurrentHeight(i),
                    interpolation);

                cell.ApplyHeight(height);

                float colorPosition = Mathf.InverseLerp(-colorHeightRange, colorHeightRange, height);
                Color color = heightGradient.Evaluate(colorPosition);
                int propertyId = ResolveColorProperty(cell);
                cell.ApplyColor(propertyBlock, propertyId, color);
            }
        }

        public void Configure(HexWaveManager targetManager, float heightRange)
        {
            manager = targetManager;
            colorHeightRange = Mathf.Max(0.0001f, heightRange);
            heightGradient = CreateDefaultGradient();
            RefreshPropertyIds();
        }

        public void RefreshBoundaryPreview()
        {
            if (manager == null)
                manager = GetComponent<HexWaveManager>();

            if (manager == null || !manager.IsInitialized)
                return;

            EnsurePropertyBlock();
            RefreshPropertyIds();
            Color restingColor = heightGradient.Evaluate(0.5f);

            for (int i = 0; i < manager.CellCount; i++)
            {
                HexCellView cell = manager.GetCellView(i);
                if (cell == null)
                    continue;

                Color color = manager.IsFixedBoundary(i) ? wallColor : restingColor;
                cell.ApplyColor(propertyBlock, ResolveColorProperty(cell), color);
            }
        }

        private int ResolveColorProperty(HexCellView cell)
        {
            Renderer targetRenderer = cell.TargetRenderer;
            if (targetRenderer != null && targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(colorPropertyId))
            {
                return colorPropertyId;
            }

            return fallbackColorPropertyId;
        }

        private void RefreshPropertyIds()
        {
            colorPropertyId = Shader.PropertyToID(colorPropertyName);
            fallbackColorPropertyId = Shader.PropertyToID("_Color");
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.32f, 0.04f), 0f),
                    new GradientColorKey(new Color(0.12f, 0.85f, 0.28f), 0.5f),
                    new GradientColorKey(new Color(0.08f, 0.38f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
