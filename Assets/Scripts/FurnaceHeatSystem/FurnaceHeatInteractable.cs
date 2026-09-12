using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Converts a left click on the 3D manual-set button into a furnace calibration toggle.
/// It uses the right split-screen camera and accepts only its own Collider as the first raycast hit.
/// </summary>
[DisallowMultipleComponent]
public sealed class FurnaceHeatInteractable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FurnaceHeatManager furnaceHeatManager;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Collider hitCollider;

    [Header("Raycast")]
    [SerializeField, Min(0f)] private float maxRayDistance = 100f;
    [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

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
        if (furnaceHeatManager == null || interactionCamera == null || hitCollider == null)
            return false;

        if (!interactionCamera.pixelRect.Contains(pointerPosition))
            return false;

        Ray ray = interactionCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastLayers, triggerInteraction))
            return false;

        return hit.collider == hitCollider && furnaceHeatManager.ToggleManualSet();
    }

    private void CacheReferences()
    {
        if (furnaceHeatManager == null)
            furnaceHeatManager = FurnaceHeatManager.Instance;
        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();
    }
}
