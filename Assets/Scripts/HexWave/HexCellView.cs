using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexCellView : MonoBehaviour
    {
        [SerializeField] private HexCoordinate coordinate;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, HideInInspector] private Vector3 baseLocalPosition;
        [SerializeField, HideInInspector] private bool fixedBoundary;

        public HexCoordinate Coordinate => coordinate;
        public Renderer TargetRenderer => targetRenderer;
        public Vector3 BaseLocalPosition => baseLocalPosition;
        public bool IsFixedBoundary => fixedBoundary;
        public int RuntimeIndex { get; private set; } = -1;

        private void Reset()
        {
            targetRenderer = GetComponentInChildren<Renderer>();
            baseLocalPosition = transform.localPosition;
        }

        private void OnValidate()
        {
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
