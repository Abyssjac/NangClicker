using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Presentation-only bridge from TasteManager's current player taste to topping GameObject roots
/// beneath NangPreview. Preference state deliberately has no visual here yet.
/// </summary>
[DisallowMultipleComponent]
public sealed class TasteVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TasteManager tasteManager;
    [SerializeField] private GameObject sesameRoot;
    [SerializeField] private GameObject cheeseRoot;
    [SerializeField] private GameObject chiliPowderRoot;
    [SerializeField] private GameObject cuminRoot;

    private bool isSubscribed;

    private void Reset()
    {
        TryResolveManager();
    }

    private void OnEnable()
    {
        TryResolveManager();
        Subscribe();
        RefreshVisual();
    }

    private void Update()
    {
        if (tasteManager == null && TryResolveManager())
        {
            Subscribe();
            RefreshVisual();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            RefreshVisual();
    }

    private bool TryResolveManager()
    {
        if (tasteManager != null)
            return true;

        tasteManager = TasteManager.Instance;
        return tasteManager != null;
    }

    private void Subscribe()
    {
        if (isSubscribed || tasteManager == null)
            return;

        tasteManager.OnCurrentSpicesChanged += HandleCurrentSpicesChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (isSubscribed && tasteManager != null)
            tasteManager.OnCurrentSpicesChanged -= HandleCurrentSpicesChanged;

        isSubscribed = false;
    }

    private void HandleCurrentSpicesChanged(IReadOnlyList<SpiceType> spices)
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (tasteManager == null)
            return;

        IReadOnlyList<SpiceType> currentSpices = tasteManager.CurrentSpices;
        SetRootActive(sesameRoot, currentSpices.Contains(SpiceType.Sesame));
        SetRootActive(cheeseRoot, currentSpices.Contains(SpiceType.Cheese));
        SetRootActive(chiliPowderRoot, currentSpices.Contains(SpiceType.ChiliPowder));
        SetRootActive(cuminRoot, currentSpices.Contains(SpiceType.Cumin));
    }

    private static void SetRootActive(GameObject root, bool shouldBeActive)
    {
        if (root != null && root.activeSelf != shouldBeActive)
            root.SetActive(shouldBeActive);
    }
}
