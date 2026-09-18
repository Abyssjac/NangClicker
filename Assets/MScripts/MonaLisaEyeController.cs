using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>Moves both pupil sprites in one shared direction toward the current gaze focus.</summary>
    public sealed class MonaLisaEyeController : MonoBehaviour
    {
        [SerializeField] private GazeFocusController gazeFocus;
        [SerializeField] private Transform eyePairPivot;
        [SerializeField] private Transform leftPupil;
        [SerializeField] private Transform rightPupil;
        [SerializeField, Min(0f)] private float pupilTravelDistance = 0.1f;

        [Header("Debug")]
        [Tooltip("Draws the exact world-space travel circles and current gaze direction in the Scene view.")]
        [SerializeField] private bool drawPupilTravelGizmos = true;

        private Vector3 leftBaseLocalPosition;
        private Vector3 rightBaseLocalPosition;
        private bool hasCachedBasePositions;

        private void Awake() => CacheBasePositions();

        private void LateUpdate()
        {
            CacheBasePositions();
            if (gazeFocus == null || eyePairPivot == null || leftPupil == null || rightPupil == null)
                return;

            Vector2 direction = gazeFocus.CurrentWorldPosition - (Vector2)eyePairPivot.position;
            Vector3 worldOffset = direction.sqrMagnitude <= Mathf.Epsilon
                ? Vector3.zero
                : (Vector3)(direction.normalized * pupilTravelDistance);

            SetPupilPosition(leftPupil, leftBaseLocalPosition, worldOffset);
            SetPupilPosition(rightPupil, rightBaseLocalPosition, worldOffset);
        }

        public void ConfigurePrototype(
            GazeFocusController focus,
            Transform pairPivot,
            Transform left,
            Transform right,
            float travelDistance)
        {
            gazeFocus = focus;
            eyePairPivot = pairPivot;
            leftPupil = left;
            rightPupil = right;
            pupilTravelDistance = Mathf.Max(0f, travelDistance);
            hasCachedBasePositions = false;
            CacheBasePositions();
        }

        private void CacheBasePositions()
        {
            if (hasCachedBasePositions || leftPupil == null || rightPupil == null)
                return;

            leftBaseLocalPosition = leftPupil.localPosition;
            rightBaseLocalPosition = rightPupil.localPosition;
            hasCachedBasePositions = true;
        }

        private static void SetPupilPosition(Transform pupil, Vector3 baseLocalPosition, Vector3 worldOffset)
        {
            Transform parent = pupil.parent;
            Vector3 localOffset = parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;
            pupil.localPosition = baseLocalPosition + localOffset;
        }

        private void OnDrawGizmos()
        {
            if (!drawPupilTravelGizmos)
                return;

            DrawPupilTravelGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawPupilTravelGizmos)
                return;

            DrawPupilTravelGizmos(true);
        }

        private void DrawPupilTravelGizmos(bool selected)
        {
            CacheBasePositions();
            if (eyePairPivot == null || leftPupil == null || rightPupil == null)
                return;

            Vector3 leftBaseWorldPosition = GetBaseWorldPosition(leftPupil, leftBaseLocalPosition);
            Vector3 rightBaseWorldPosition = GetBaseWorldPosition(rightPupil, rightBaseLocalPosition);
            Color rangeColor = new(1f, 0.18f, 0.8f, selected ? 1f : 0.72f);
            DrawWireCircle(leftBaseWorldPosition, pupilTravelDistance, rangeColor);
            DrawWireCircle(rightBaseWorldPosition, pupilTravelDistance, rangeColor);

            Gizmos.color = rangeColor;
            Gizmos.DrawSphere(leftBaseWorldPosition, selected ? 0.025f : 0.016f);
            Gizmos.DrawSphere(rightBaseWorldPosition, selected ? 0.025f : 0.016f);

            if (gazeFocus == null)
                return;

            Vector3 focusPosition = gazeFocus.CurrentWorldPosition;
            Gizmos.color = new Color(0.05f, 0.94f, 1f, selected ? 1f : 0.7f);
            Gizmos.DrawLine(eyePairPivot.position, focusPosition);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(leftPupil.position, selected ? 0.022f : 0.014f);
            Gizmos.DrawSphere(rightPupil.position, selected ? 0.022f : 0.014f);
        }

        private static Vector3 GetBaseWorldPosition(Transform pupil, Vector3 baseLocalPosition)
        {
            Transform parent = pupil.parent;
            return parent != null ? parent.TransformPoint(baseLocalPosition) : baseLocalPosition;
        }

        private static void DrawWireCircle(Vector3 center, float radius, Color color)
        {
            const int segmentCount = 32;
            Gizmos.color = color;

            Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / segmentCount;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}
