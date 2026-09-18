using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>
    /// Owns the current world-space focus position. Its transform is the visible focus marker and is always
    /// written directly from the data position; no interpolation is applied.
    /// </summary>
    public sealed class GazeFocusController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("A MonoBehaviour that implements IGazeInputProvider. Unity cannot serialize an interface field directly.")]
        [SerializeField] private MonoBehaviour inputProviderBehaviour;

        [Header("Movement")]
        [SerializeField] private GazeArea gazeArea = new(new Vector2(-7f, -5f), new Vector2(7f, 0.5f));
        [SerializeField, Min(0f)] private float moveSpeed = 6f;

        [Header("Debug")]
        [Tooltip("Draws the exact world-space GazeArea used to clamp the focus and tourist wait positions.")]
        [SerializeField] private bool drawGazeAreaGizmo = true;

        private IGazeInputProvider inputProvider;
        private bool reportedInvalidProvider;

        public Vector2 CurrentWorldPosition => transform.position;
        public GazeArea Area => gazeArea;

        private void Awake()
        {
            ResolveInputProvider();
            SetWorldPosition(transform.position);
        }

        private void Update()
        {
            ResolveInputProvider();
            if (inputProvider == null)
                return;

            Vector2 movementDirection = Vector2.ClampMagnitude(inputProvider.GetCurrentInput(), 1f);
            Vector2 nextPosition = CurrentWorldPosition + movementDirection * (moveSpeed * Time.deltaTime);
            SetWorldPosition(nextPosition);
        }

        public void SetWorldPosition(Vector2 worldPosition)
        {
            Vector2 clampedPosition = gazeArea.Clamp(worldPosition);
            transform.position = new Vector3(clampedPosition.x, clampedPosition.y, transform.position.z);
        }

        public void ConfigurePrototype(MonoBehaviour provider, GazeArea area, float speed)
        {
            inputProviderBehaviour = provider;
            gazeArea = area;
            moveSpeed = Mathf.Max(0f, speed);
            inputProvider = provider as IGazeInputProvider;
            SetWorldPosition(transform.position);
        }

        private void ResolveInputProvider()
        {
            if (inputProviderBehaviour == null)
            {
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is not IGazeInputProvider)
                        continue;

                    inputProviderBehaviour = behaviours[i];
                    break;
                }
            }

            inputProvider = inputProviderBehaviour as IGazeInputProvider;
            if (inputProvider != null || reportedInvalidProvider)
                return;

            reportedInvalidProvider = true;
            Debug.LogWarning(
                $"[{nameof(GazeFocusController)}] Assign a MonoBehaviour that implements {nameof(IGazeInputProvider)}.",
                this);
        }

        private void OnDrawGizmos()
        {
            if (!drawGazeAreaGizmo)
                return;

            DrawGazeAreaGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGazeAreaGizmo)
                return;

            DrawGazeAreaGizmo(true);
        }

        private void DrawGazeAreaGizmo(bool selected)
        {
            Vector2 center = gazeArea.Center;
            Vector2 size = gazeArea.Size;
            Vector3 gizmoCenter = new(center.x, center.y, transform.position.z);
            Vector3 gizmoSize = new(size.x, size.y, 0.01f);

            Gizmos.color = new Color(0.04f, 0.85f, 1f, selected ? 0.11f : 0.045f);
            Gizmos.DrawCube(gizmoCenter, gizmoSize);
            Gizmos.color = new Color(0.04f, 0.9f, 1f, selected ? 1f : 0.72f);
            Gizmos.DrawWireCube(gizmoCenter, gizmoSize);
        }
    }
}
