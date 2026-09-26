using UnityEngine;

namespace NangClicker.HexWave
{
    [DisallowMultipleComponent]
    public sealed class HexEnemyDirectionArrowView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private LineRenderer lineRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Transform sourceCell;
        private Transform destinationCell;
        private Color arrowColor;
        private float anchorLocalY;
        private float endPadding;

        public int StackId { get; private set; } = -1;

        public void Initialize(
            int stackId,
            Transform source,
            Transform destination,
            Material material,
            Color color,
            float anchorHeight,
            float width,
            float padding)
        {
            StackId = stackId;
            sourceCell = source;
            destinationCell = destination;
            arrowColor = color;
            anchorLocalY = Mathf.Max(0f, anchorHeight);
            endPadding = Mathf.Max(0f, padding);
            propertyBlock ??= new MaterialPropertyBlock();

            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.widthMultiplier = Mathf.Max(0.01f, width);
            lineRenderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.15f),
                new Keyframe(0.08f, 0.4f),
                new Keyframe(0.7f, 0.4f),
                new Keyframe(0.72f, 1f),
                new Keyframe(1f, 0f));

            if (material != null)
                lineRenderer.sharedMaterial = material;

            SetOpacity(color.a);
            UpdatePositions();
        }

        public void RefreshAnchors(Transform source, Transform destination)
        {
            sourceCell = source;
            destinationCell = destination;
            UpdatePositions();
        }

        public void SetOpacity(float opacity)
        {
            if (lineRenderer == null)
                return;

            Color color = arrowColor;
            color.a = Mathf.Clamp01(opacity);
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lineRenderer.SetPropertyBlock(propertyBlock);
        }

        public void UpdatePositions()
        {
            if (lineRenderer == null || sourceCell == null || destinationCell == null)
                return;

            Vector3 source = sourceCell.TransformPoint(new Vector3(0f, anchorLocalY, 0f));
            Vector3 destination = destinationCell.TransformPoint(new Vector3(0f, anchorLocalY, 0f));
            Vector3 offset = destination - source;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            Vector3 direction = offset / distance;
            float appliedPadding = Mathf.Min(endPadding, distance * 0.25f);
            lineRenderer.SetPosition(0, source + direction * appliedPadding);
            lineRenderer.SetPosition(1, destination - direction * appliedPadding);
        }
    }
}
