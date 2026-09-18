using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Sprite-only presentation for a photo point. Gameplay decisions remain in PhotoPointManager.</summary>
    public sealed class PhotoPointView : MonoBehaviour
    {
        [Header("Range")]
        [SerializeField] private SpriteRenderer rangeRenderer;
        [SerializeField] private Color unfocusedRangeColor = new(1f, 0.77f, 0.18f, 0.28f);
        [SerializeField] private Color focusedRangeColor = new(1f, 0.5f, 0.08f, 0.48f);

        [Header("Bars")]
        [SerializeField] private SpriteRenderer patienceFill;
        [SerializeField] private SpriteRenderer focusFill;
        [SerializeField, Min(0.01f)] private float barWidth = 1.2f;
        [SerializeField, Min(0.01f)] private float barHeight = 0.1f;

        private float patienceY;
        private float focusY;
        private bool hasCachedBarPositions;

        private void Awake() => CacheBarPositions();

        public void Setup(float hitRadius, float patience01, float focus01, PhotoPointState state)
        {
            CacheBarPositions();
            SetHitRadius(hitRadius);
            SetPatience01(patience01);
            SetFocus01(focus01);
            SetState(state);
        }

        public void SetHitRadius(float radius)
        {
            if (rangeRenderer == null)
                return;

            float diameter = Mathf.Max(0f, radius) * 2f;
            rangeRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        public void SetPatience01(float value) => SetBarFill(patienceFill, patienceY, value);
        public void SetFocus01(float value) => SetBarFill(focusFill, focusY, value);

        public void SetState(PhotoPointState state)
        {
            if (rangeRenderer != null)
                rangeRenderer.color = state == PhotoPointState.Focused ? focusedRangeColor : unfocusedRangeColor;
        }

        public void ConfigurePrototype(
            SpriteRenderer range,
            SpriteRenderer patience,
            SpriteRenderer focus,
            float configuredBarWidth,
            float configuredBarHeight)
        {
            rangeRenderer = range;
            patienceFill = patience;
            focusFill = focus;
            barWidth = Mathf.Max(0.01f, configuredBarWidth);
            barHeight = Mathf.Max(0.01f, configuredBarHeight);
            hasCachedBarPositions = false;
            CacheBarPositions();
        }

        private void CacheBarPositions()
        {
            if (hasCachedBarPositions)
                return;

            patienceY = patienceFill != null ? patienceFill.transform.localPosition.y : 0f;
            focusY = focusFill != null ? focusFill.transform.localPosition.y : 0f;
            hasCachedBarPositions = true;
        }

        private void SetBarFill(SpriteRenderer fill, float y, float normalizedValue)
        {
            if (fill == null)
                return;

            float value = Mathf.Clamp01(normalizedValue);
            Transform fillTransform = fill.transform;
            fillTransform.localScale = new Vector3(barWidth * value, barHeight, 1f);
            fillTransform.localPosition = new Vector3(-barWidth * (1f - value) * 0.5f, y, fillTransform.localPosition.z);
        }
    }
}
