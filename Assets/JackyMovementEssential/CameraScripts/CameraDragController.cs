using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(CameraBase))]
public class CameraDragController : MonoBehaviour, IReliedCameraComponent
{
    [Header("References")]
    [SerializeField] private CameraBase reliedCamera;

    [Tooltip("Defines the drag plane: local +Z is the camera's fixed facing direction, while local X/Y are the pan axes.")]
    [SerializeField] private Transform panSpace;

    [Header("Pan Bounds (Pan Space Local X/Y)")]
    [SerializeField] private Vector2 minLocalPosition = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 maxLocalPosition = new Vector2(10f, 10f);

    [FormerlySerializedAs("moveCameraWithPointer")]
    [Tooltip("When enabled, dragging the mouse right moves the viewed content right, like grabbing the scene directly.")]
    [SerializeField] private bool contentFollowsPointer = true;

    [Header("Scene View Gizmos")]
    [Tooltip("Master switch for all pan-bound gizmos.")]
    [SerializeField] private bool drawBoundsGizmo = true;

    [Tooltip("Draw gizmos even when this controller is not selected. Leave disabled to reduce Scene View clutter.")]
    [SerializeField] private bool showGizmosWhenUnselected;

    [Tooltip("Draws the white rectangle that the camera currently captures on the Pan Space plane.")]
    [SerializeField] private bool drawCurrentViewFrame = true;

    [Tooltip("Draws the yellow area that can be captured while the camera center remains inside Pan Bounds.")]
    [SerializeField] private bool drawTotalCoverageArea = true;

    public CameraBase ReliedCamera => reliedCamera;

    private Camera cachedCamera;
    private bool isDragging;
    private Vector3 lastDragPoint;
    private float lockedLocalDepth;

    private void Reset()
    {
        reliedCamera = GetComponent<CameraBase>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        isDragging = false;

        if (panSpace != null)
            lockedLocalDepth = panSpace.InverseTransformPoint(transform.position).z;
    }

    private void OnDisable()
    {
        isDragging = false;
    }

    private void Update()
    {
        if (cachedCamera == null || panSpace == null)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 pointerPosition = mouse.position.ReadValue();

        if (!isDragging)
        {
            if (mouse.middleButton.wasPressedThisFrame && IsInsideCameraViewport(pointerPosition))
                TryBeginDrag(pointerPosition);

            return;
        }

        if (!mouse.middleButton.isPressed)
        {
            isDragging = false;
            return;
        }

        if (!TryGetDragPoint(pointerPosition, out Vector3 currentDragPoint))
            return;

        Vector3 worldDelta = currentDragPoint - lastDragPoint;
        if (contentFollowsPointer)
            worldDelta = -worldDelta;

        PanCamera(worldDelta);

        // The camera has moved, so the same screen position now maps to a different point
        // on the drag plane. Refresh the anchor after movement (and after bounds clamping)
        // to prevent the next frame from applying an equal-and-opposite correction.
        if (!TryGetDragPoint(pointerPosition, out lastDragPoint))
            isDragging = false;
    }

    /// <summary>
    /// Lets a view-state controller replace the active pan area when the game switches views.
    /// </summary>
    public void SetPanBounds(Vector2 minLocal, Vector2 maxLocal)
    {
        minLocalPosition = minLocal;
        maxLocalPosition = maxLocal;
        ClampCurrentPosition();
    }

    /// <summary>
    /// Re-captures the fixed local Z depth after another system positions the camera.
    /// </summary>
    public void CaptureCurrentDepth()
    {
        if (panSpace != null)
            lockedLocalDepth = panSpace.InverseTransformPoint(transform.position).z;
    }

    private void CacheReferences()
    {
        if (reliedCamera == null)
            reliedCamera = GetComponent<CameraBase>();

        cachedCamera = reliedCamera != null && reliedCamera.CachedCamera != null
            ? reliedCamera.CachedCamera
            : GetComponent<Camera>();
    }

    private bool IsInsideCameraViewport(Vector2 pointerPosition)
    {
        return cachedCamera.pixelRect.Contains(pointerPosition);
    }

    private void TryBeginDrag(Vector2 pointerPosition)
    {
        if (!TryGetDragPoint(pointerPosition, out lastDragPoint))
            return;

        isDragging = true;
    }

    private bool TryGetDragPoint(Vector2 pointerPosition, out Vector3 point)
    {
        Plane dragPlane = new Plane(panSpace.forward, panSpace.position);
        Ray pointerRay = cachedCamera.ScreenPointToRay(pointerPosition);

        if (dragPlane.Raycast(pointerRay, out float enter))
        {
            point = pointerRay.GetPoint(enter);
            return true;
        }

        point = default;
        return false;
    }

    private void PanCamera(Vector3 worldDelta)
    {
        Vector3 planarDelta = Vector3.ProjectOnPlane(worldDelta, panSpace.forward);
        Vector3 localPosition = panSpace.InverseTransformPoint(transform.position + planarDelta);

        localPosition.x = Mathf.Clamp(localPosition.x, Mathf.Min(minLocalPosition.x, maxLocalPosition.x), Mathf.Max(minLocalPosition.x, maxLocalPosition.x));
        localPosition.y = Mathf.Clamp(localPosition.y, Mathf.Min(minLocalPosition.y, maxLocalPosition.y), Mathf.Max(minLocalPosition.y, maxLocalPosition.y));
        localPosition.z = lockedLocalDepth;

        transform.position = panSpace.TransformPoint(localPosition);
    }

    private void ClampCurrentPosition()
    {
        if (panSpace == null)
            return;

        Vector3 localPosition = panSpace.InverseTransformPoint(transform.position);
        localPosition.x = Mathf.Clamp(localPosition.x, Mathf.Min(minLocalPosition.x, maxLocalPosition.x), Mathf.Max(minLocalPosition.x, maxLocalPosition.x));
        localPosition.y = Mathf.Clamp(localPosition.y, Mathf.Min(minLocalPosition.y, maxLocalPosition.y), Mathf.Max(minLocalPosition.y, maxLocalPosition.y));
        localPosition.z = lockedLocalDepth;
        transform.position = panSpace.TransformPoint(localPosition);
    }

    private void OnDrawGizmos()
    {
        if (showGizmosWhenUnselected)
            DrawPanGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmosWhenUnselected)
            DrawPanGizmos();
    }

    private void DrawPanGizmos()
    {
        if (!drawBoundsGizmo || panSpace == null)
            return;

        Camera gizmoCamera = cachedCamera != null ? cachedCamera : GetComponent<Camera>();
        if (gizmoCamera == null)
            return;

        float minX = Mathf.Min(minLocalPosition.x, maxLocalPosition.x);
        float maxX = Mathf.Max(minLocalPosition.x, maxLocalPosition.x);
        float minY = Mathf.Min(minLocalPosition.y, maxLocalPosition.y);
        float maxY = Mathf.Max(minLocalPosition.y, maxLocalPosition.y);
        float localDepth = panSpace.InverseTransformPoint(transform.position).z;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = panSpace.localToWorldMatrix;

        if (TryGetViewOffsetsOnPanPlane(gizmoCamera, out Vector2 viewMinOffset, out Vector2 viewMaxOffset))
        {
            if (drawTotalCoverageArea)
            {
                Vector2 coverageMin = new Vector2(minX, minY) + viewMinOffset;
                Vector2 coverageMax = new Vector2(maxX, maxY) + viewMaxOffset;

                Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.08f);
                DrawLocalRectangle(coverageMin, coverageMax, localDepth, true);
                Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
                DrawLocalRectangle(coverageMin, coverageMax, localDepth, false);
            }

            if (drawCurrentViewFrame)
            {
                Vector3 localCameraPosition = panSpace.InverseTransformPoint(transform.position);
                Vector2 currentMin = new Vector2(localCameraPosition.x, localCameraPosition.y) + viewMinOffset;
                Vector2 currentMax = new Vector2(localCameraPosition.x, localCameraPosition.y) + viewMaxOffset;

                Gizmos.color = Color.white;
                DrawLocalRectangle(currentMin, currentMax, localDepth, false);
            }
        }

        Gizmos.color = Color.cyan;
        DrawLocalRectangle(new Vector2(minX, minY), new Vector2(maxX, maxY), localDepth, false);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private bool TryGetViewOffsetsOnPanPlane(Camera sourceCamera, out Vector2 minOffset, out Vector2 maxOffset)
    {
        Plane dragPlane = new Plane(panSpace.forward, panSpace.position);
        Vector3 localCameraPosition = panSpace.InverseTransformPoint(transform.position);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        Vector2[] viewportCorners =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
        };

        foreach (Vector2 viewportCorner in viewportCorners)
        {
            Ray viewRay = sourceCamera.ViewportPointToRay(new Vector3(viewportCorner.x, viewportCorner.y, 0f));
            if (!dragPlane.Raycast(viewRay, out float enter))
            {
                minOffset = default;
                maxOffset = default;
                return false;
            }

            Vector3 localViewPoint = panSpace.InverseTransformPoint(viewRay.GetPoint(enter));
            minX = Mathf.Min(minX, localViewPoint.x);
            maxX = Mathf.Max(maxX, localViewPoint.x);
            minY = Mathf.Min(minY, localViewPoint.y);
            maxY = Mathf.Max(maxY, localViewPoint.y);
        }

        minOffset = new Vector2(minX - localCameraPosition.x, minY - localCameraPosition.y);
        maxOffset = new Vector2(maxX - localCameraPosition.x, maxY - localCameraPosition.y);
        return true;
    }

    private static void DrawLocalRectangle(Vector2 min, Vector2 max, float depth, bool filled)
    {
        float minX = Mathf.Min(min.x, max.x);
        float maxX = Mathf.Max(min.x, max.x);
        float minY = Mathf.Min(min.y, max.y);
        float maxY = Mathf.Max(min.y, max.y);

        Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, depth);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0.02f);

        if (filled)
            Gizmos.DrawCube(center, size);
        else
            Gizmos.DrawWireCube(center, size);
    }
}
