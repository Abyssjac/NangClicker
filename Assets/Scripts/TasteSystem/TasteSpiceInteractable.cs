using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A 3D counter-spice interaction. It accepts a left click only when this exact Collider is the
/// first right-panel-camera hit, then asks TasteManager to add its configured spice.
/// </summary>
[DisallowMultipleComponent]
public sealed class TasteSpiceInteractable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TasteManager tasteManager;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Collider hitCollider;

    [Header("Spice")]
    [SerializeField] private SpiceType spiceType;

    [Header("Raycast")]
    [SerializeField, Min(0f)] private float maxRayDistance = 100f;
    [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    public SpiceType SpiceType => spiceType;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        TryHandlePointerClick(mouse.position.ReadValue());
    }

    public bool TryHandlePointerClick(Vector2 pointerPosition)
    {
        if (tasteManager == null || interactionCamera == null || hitCollider == null || spiceType == SpiceType.None)
            return false;

        if (!interactionCamera.pixelRect.Contains(pointerPosition))
            return false;

        Ray ray = interactionCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastLayers, triggerInteraction))
            return false;

        return hit.collider == hitCollider && tasteManager.AddSpice(spiceType);
    }

    private void CacheReferences()
    {
        if (tasteManager == null)
            tasteManager = TasteManager.Instance;
        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();
    }
}
