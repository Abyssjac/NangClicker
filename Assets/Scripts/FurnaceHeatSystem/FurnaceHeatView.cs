using UnityEngine;

/// <summary>
/// World-space presentation for FurnaceHeatManager. This component never changes score or heat state.
/// </summary>
[DisallowMultipleComponent]
public sealed class FurnaceHeatView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FurnaceHeatManager furnaceHeatManager;
    [SerializeField] private Transform trackLeft;
    [SerializeField] private Transform trackRight;
    [SerializeField] private Transform heatPointer;
    [SerializeField] private Transform profitableRangeVisual;
    [SerializeField] private Renderer heatPointerRenderer;
    [SerializeField] private Renderer manualSetButtonRenderer;

    [Header("Colours")]
    [SerializeField] private Color inactivePointerColor = new(0.65f, 0.25f, 0.08f, 1f);
    [SerializeField] private Color profitablePointerColor = new(1f, 0.82f, 0.16f, 1f);
    [SerializeField] private Color idleButtonColor = new(0.32f, 0.12f, 0.05f, 1f);
    [SerializeField] private Color calibratingButtonColor = new(0.95f, 0.31f, 0.06f, 1f);

    private MaterialPropertyBlock materialPropertyBlock;
    private bool isSubscribed;

    private void Reset()
    {
        TryResolveManager();
    }

    private void Awake()
    {
        materialPropertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        TryResolveManager();
        Subscribe();
        RefreshVisual();
    }

    private void Update()
    {
        if (furnaceHeatManager == null && TryResolveManager())
        {
            Subscribe();
            RefreshVisual();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshVisual();
    }

    private bool TryResolveManager()
    {
        if (furnaceHeatManager != null)
            return true;

        furnaceHeatManager = FurnaceHeatManager.Instance;
        return furnaceHeatManager != null;
    }

    private void Subscribe()
    {
        if (isSubscribed || furnaceHeatManager == null)
            return;

        furnaceHeatManager.OnHeatStateChanged += RefreshVisual;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (isSubscribed && furnaceHeatManager != null)
            furnaceHeatManager.OnHeatStateChanged -= RefreshVisual;

        isSubscribed = false;
    }

    private void RefreshVisual()
    {
        if (furnaceHeatManager == null || trackLeft == null || trackRight == null)
            return;

        Vector3 trackStart = trackLeft.position;
        Vector3 trackEnd = trackRight.position;

        if (heatPointer != null)
            heatPointer.position = Vector3.Lerp(trackStart, trackEnd, furnaceHeatManager.NormalizedHeat);

        if (profitableRangeVisual != null)
        {
            Vector3 profitableStart = Vector3.Lerp(trackStart, trackEnd, furnaceHeatManager.ProfitableHeatMin / 100f);
            Vector3 profitableEnd = Vector3.Lerp(trackStart, trackEnd, furnaceHeatManager.ProfitableHeatMax / 100f);
            Vector3 rangeDirection = profitableEnd - profitableStart;

            profitableRangeVisual.position = (profitableStart + profitableEnd) * 0.5f;
            if (rangeDirection.sqrMagnitude > 0.0001f)
                profitableRangeVisual.rotation = Quaternion.FromToRotation(Vector3.right, rangeDirection.normalized);

            Vector3 rangeScale = profitableRangeVisual.localScale;
            rangeScale.x = rangeDirection.magnitude;
            profitableRangeVisual.localScale = rangeScale;
        }

        SetRendererColor(
            heatPointerRenderer,
            furnaceHeatManager.IsInProfitableRange ? profitablePointerColor : inactivePointerColor);
        SetRendererColor(
            manualSetButtonRenderer,
            furnaceHeatManager.IsCalibrating ? calibratingButtonColor : idleButtonColor);
    }

    private void SetRendererColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null || materialPropertyBlock == null)
            return;

        materialPropertyBlock.Clear();
        if (targetRenderer.sharedMaterial.HasProperty("_BaseColor"))
            materialPropertyBlock.SetColor("_BaseColor", color);
        if (targetRenderer.sharedMaterial.HasProperty("_Color"))
            materialPropertyBlock.SetColor("_Color", color);

        targetRenderer.SetPropertyBlock(materialPropertyBlock);
    }
}
