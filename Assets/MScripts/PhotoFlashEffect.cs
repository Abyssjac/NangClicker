using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>One-shot visual feedback for a successful tourist photograph.</summary>
    [DisallowMultipleComponent]
    public sealed class PhotoFlashEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer flashRenderer;

        [Header("Lifetime")]
        [SerializeField, Min(0.01f)] private float duration = 0.38f;
        [SerializeField, Min(0f)] private float startScale = 1.3f;
        [SerializeField, Min(0f)] private float endScale = 0.18f;

        private Color baseColor = Color.white;
        private float elapsed;
        private bool isPlaying;

        private void Awake()
        {
            if (flashRenderer == null)
                flashRenderer = GetComponent<SpriteRenderer>();

            if (flashRenderer != null)
                baseColor = flashRenderer.color;
        }

        private void Update()
        {
            if (!isPlaying)
                return;

            elapsed += Time.deltaTime;
            float life01 = Mathf.Clamp01(elapsed / duration);
            float currentScale = startScale + (endScale - startScale) * life01;
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            if (flashRenderer != null)
            {
                Color color = baseColor;
                color.a = baseColor.a * (1f - life01);
                flashRenderer.color = color;
            }

            if (life01 >= 1f)
                Destroy(gameObject);
        }

        public void PlayAt(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            elapsed = 0f;
            isPlaying = true;
            transform.localScale = new Vector3(startScale, startScale, 1f);

            if (flashRenderer != null)
                flashRenderer.color = baseColor;
        }

        public void ConfigurePrototype(SpriteRenderer renderer, float configuredDuration, float configuredStartScale, float configuredEndScale)
        {
            flashRenderer = renderer;
            duration = Mathf.Max(0.01f, configuredDuration);
            startScale = Mathf.Max(0f, configuredStartScale);
            endScale = Mathf.Max(0f, configuredEndScale);
            baseColor = flashRenderer != null ? flashRenderer.color : Color.white;
        }
    }
}
