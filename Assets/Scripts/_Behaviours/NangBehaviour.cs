using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Data for one successful manual Nang press. Values are kept in world space so a visual
/// child can convert them against its own pivot without depending on the click Collider.
/// </summary>
public readonly struct NangPressInfo
{
    public NangPressInfo(Vector3 worldPoint, Vector3 worldNormal, double queuedNangAmount)
    {
        WorldPoint = worldPoint;
        WorldNormal = worldNormal;
        QueuedNangAmount = queuedNangAmount;
    }

    public Vector3 WorldPoint { get; }
    public Vector3 WorldNormal { get; }
    public double QueuedNangAmount { get; }
}

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

    /// <summary>
    /// Raised only after this click has successfully been queued as manual Nang production.
    /// Visual listeners can use the hit position without owning input or score logic.
    /// </summary>
    public event Action<NangPressInfo> OnNangPressed;

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
        if (manager == null || !manager.QueueManualNangClick())
            return false;

        OnNangPressed?.Invoke(new NangPressInfo(hit.point, hit.normal, manager.ManualNangPerClick));
        return true;
    }

    private void CacheLocalCollider()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();
    }
}
