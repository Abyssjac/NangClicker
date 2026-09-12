using UnityEngine;

/// <summary>
/// Owns the Upgrade main panel's lifecycle and routes its hotkey through AllUIManager.
/// The controller stays outside the visual panel so it remains available while the panel is closed.
/// </summary>
[DisallowMultipleComponent]
public sealed class UpgradePanelToggle : MonoBehaviour, IGeneralPanelOwner
{
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    public bool IsOpen => AllUIManager.Instance != null && AllUIManager.Instance.IsTopPanel(this);

    private void Awake()
    {
        SetPanelVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Toggle()
    {
        if (upgradePanel == null)
        {
            Debug.LogWarning($"[{nameof(UpgradePanelToggle)}] No upgrade panel is assigned.", this);
            return;
        }

        AllUIManager manager = AllUIManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(UpgradePanelToggle)}] No {nameof(AllUIManager)} is available.", this);
            return;
        }

        if (manager.IsTopPanel(this))
        {
            manager.RequestClose(this);
            return;
        }

        // A Stack panel that is already below another open panel must be restored by the
        // manager's normal Escape flow; opening it again would create a duplicate stack entry.
        if (!manager.IsPanelOpen(this))
            manager.RequestOpen(this, PanelOpenType.Stack);
    }

    /// <summary>Called by AllUIManager after this main panel has been pushed to the UI stack.</summary>
    public void OnPanelOpenRequested()
    {
        SetPanelVisible(true);
    }

    /// <summary>Called by AllUIManager when this panel is popped by Escape or a UI close action.</summary>
    public void OnPanelCloseRequested()
    {
        SetPanelVisible(false);
    }

    private void SetPanelVisible(bool visible)
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(visible);
    }
}
