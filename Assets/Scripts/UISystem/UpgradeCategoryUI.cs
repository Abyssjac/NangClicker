using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only layout controller for one upgrade category. A category is visible only
/// while at least one of its fixed child slots is visible.
/// </summary>
[DisallowMultipleComponent]
public sealed class UpgradeCategoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform slotsGrid;
    [SerializeField] private RectTransform layoutRoot;

    private UpgradeSlotBehaviour[] slots = System.Array.Empty<UpgradeSlotBehaviour>();

    public void Initialize()
    {
        if (slotsGrid == null)
            slotsGrid = GetComponentInChildren<GridLayoutGroup>(true)?.GetComponent<RectTransform>();

        if (layoutRoot == null)
            layoutRoot = transform as RectTransform;

        slots = slotsGrid != null
            ? slotsGrid.GetComponentsInChildren<UpgradeSlotBehaviour>(true)
            : System.Array.Empty<UpgradeSlotBehaviour>();
    }

    /// <summary>
    /// Shows this category only when one or more child Slots were bound by UpgradeManagerUI.
    /// activeSelf is deliberate: it still detects a newly-bound slot when this Category was
    /// hidden during the previous snapshot refresh.
    /// </summary>
    public void RefreshVisibility()
    {
        Initialize();

        bool hasVisibleSlot = false;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].gameObject.activeSelf)
            {
                hasVisibleSlot = true;
                break;
            }
        }

        if (gameObject.activeSelf != hasVisibleSlot)
            gameObject.SetActive(hasVisibleSlot);
    }

    /// <summary>
    /// Rebuilds from the inner Grid outward after all Slot active states have settled.
    /// </summary>
    public void RebuildLayout()
    {
        if (!gameObject.activeSelf)
            return;

        if (slotsGrid != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotsGrid);

        if (layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }
}
