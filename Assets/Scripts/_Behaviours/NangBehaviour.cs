using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Converts a left-click on this Nang's designated 3D Collider into one queued manual Nang click.
/// Production, score, and future resource validation remain owned by ScoreManager and other systems.
/// </summary>
[DisallowMultipleComponent]
public class NangBehaviour : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The split-screen camera that renders this Nang. Only clicks inside this camera's viewport are considered.")]
    [SerializeField] private Camera interactionCamera;

    [Tooltip("Only a ray whose first hit is this exact Collider counts as a Nang click.")]
    [SerializeField] private Collider hitCollider;

    [Header("Raycast")]
    [SerializeField, Min(0f)] private float maxRayDistance = 100f;
    [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    public Camera InteractionCamera => interactionCamera;
    public Collider HitCollider => hitCollider;

    private void Reset()
    {
        CacheLocalCollider();
    }

    private void OnValidate()
    {
        CacheLocalCollider();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        TryHandlePointerClick(mouse.position.ReadValue());
    }

    /// <summary>
    /// Allows other input routes to use the same hit validation, while returning whether a manual click was queued.
    /// </summary>
    public bool TryHandlePointerClick(Vector2 pointerPosition)
    {
        if (interactionCamera == null || hitCollider == null)
            return false;

        if (!interactionCamera.pixelRect.Contains(pointerPosition))
            return false;

        Ray ray = interactionCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastLayers, triggerInteraction))
            return false;

        if (hit.collider != hitCollider)
            return false;

        ScoreManager manager = ScoreManager.Instance;
        return manager != null && manager.QueueManualNangClick();
    }

    private void CacheLocalCollider()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();
    }
}
