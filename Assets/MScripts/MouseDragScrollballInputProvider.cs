using UnityEngine;

namespace MonaLisaGame
{
    /// <summary>
    /// A world-space virtual trackball. It produces input only while the player holds the left mouse button
    /// after pressing this object's Collider, and rolls its visual directly from the raw mouse delta.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MouseDragScrollballInputProvider : MonoBehaviour, IGazeInputProvider
    {
        [Header("Input")]
        [Tooltip("Mouse pixels moved in one frame that represent a full-strength gaze direction.")]
        [SerializeField, Min(1f)] private float pixelsPerFullInput = 85f;
        [SerializeField] private bool invertY;

        [Header("3D Visual")]
        [Tooltip("The transform that visibly rolls. Defaults to this GameObject when left empty.")]
        [SerializeField] private Transform ballVisual;
        [SerializeField] private Camera inputCamera;
        [Tooltip("Visual roll amount applied per mouse pixel. This never smooths or lags the motion.")]
        [SerializeField, Min(0f)] private float rollDegreesPerPixel = 0.42f;

        private bool isDragging;
        private Vector2 lastMousePosition;
        private Vector2 currentInput;

        /// <summary>True from a valid left-button press on the scrollball until that button is released.</summary>
        public bool IsDragging => isDragging;

        public Vector2 GetCurrentInput() => currentInput;

        private void Awake()
        {
            if (ballVisual == null)
                ballVisual = transform;
        }

        private void Update()
        {
            currentInput = Vector2.zero;
            if (!isDragging)
                return;

            if (!Input.GetMouseButton(0))
            {
                EndDrag();
                return;
            }

            Vector2 mousePosition = Input.mousePosition;
            Vector2 mouseDelta = mousePosition - lastMousePosition;
            lastMousePosition = mousePosition;
            if (mouseDelta.sqrMagnitude <= Mathf.Epsilon)
                return;

            Vector2 gazeDelta = mouseDelta / pixelsPerFullInput;
            if (invertY)
                gazeDelta.y = -gazeDelta.y;

            currentInput = Vector2.ClampMagnitude(gazeDelta, 1f);
            RollVisual(mouseDelta);
        }

        private void OnMouseDown()
        {
            if (!Input.GetMouseButton(0))
                return;

            isDragging = true;
            currentInput = Vector2.zero;
            lastMousePosition = Input.mousePosition;
        }

        private void OnMouseUp() => EndDrag();

        private void OnDisable() => EndDrag();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                EndDrag();
        }

        public void ConfigurePrototype(Camera configuredInputCamera, Transform configuredBallVisual, float configuredPixelsPerFullInput, float configuredRollDegreesPerPixel)
        {
            inputCamera = configuredInputCamera;
            ballVisual = configuredBallVisual != null ? configuredBallVisual : transform;
            pixelsPerFullInput = Mathf.Max(1f, configuredPixelsPerFullInput);
            rollDegreesPerPixel = Mathf.Max(0f, configuredRollDegreesPerPixel);
        }

        private void EndDrag()
        {
            isDragging = false;
            currentInput = Vector2.zero;
        }

        private void RollVisual(Vector2 mouseDelta)
        {
            if (ballVisual == null || rollDegreesPerPixel <= 0f)
                return;

            Vector3 horizontalAxis = inputCamera != null ? inputCamera.transform.up : Vector3.up;
            Vector3 verticalAxis = inputCamera != null ? inputCamera.transform.right : Vector3.right;
            Quaternion horizontalRoll = Quaternion.AngleAxis(-mouseDelta.x * rollDegreesPerPixel, horizontalAxis);
            Quaternion verticalRoll = Quaternion.AngleAxis(mouseDelta.y * rollDegreesPerPixel, verticalAxis);
            ballVisual.rotation = horizontalRoll * verticalRoll * ballVisual.rotation;
        }
    }
}
