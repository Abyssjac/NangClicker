using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fixed visual slot in the upgrade screen. Its SlotId is an Inspector identity,
/// not a dynamic container index.
/// </summary>
public class UpgradeSlotBehaviour : MonoBehaviour
{
    [SerializeField] private UpgradeSlotId slotId;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject maxOverlay;
    [SerializeField] private Button selectionButton;

    private Action<UpgradeSlotId> onSelected;

    public UpgradeSlotId SlotId => slotId;

    private void Awake()
    {
        selectionButton ??= GetComponent<Button>();
        if (selectionButton != null)
            selectionButton.onClick.AddListener(Select);
    }

    private void OnDestroy()
    {
        if (selectionButton != null)
            selectionButton.onClick.RemoveListener(Select);
    }

    public void SetSelectionCallback(Action<UpgradeSlotId> callback)
    {
        onSelected = callback;
    }

    public void Bind(UpgradeSlotSnapshot snapshot)
    {
        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = snapshot.Icon;
            iconImage.enabled = snapshot.Icon != null;
        }

        if (maxOverlay != null)
            maxOverlay.SetActive(snapshot.IsCompleted);
    }

    public void Clear()
    {
        if (maxOverlay != null)
            maxOverlay.SetActive(false);

        gameObject.SetActive(false);
    }

    private void Select()
    {
        onSelected?.Invoke(slotId);
    }
}
