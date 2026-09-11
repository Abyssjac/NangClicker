using UnityEngine;

/// <summary>
/// Keeps the upgrade screen visibility input outside the screen itself, so a closed
/// panel can always be opened again.
/// </summary>
[DisallowMultipleComponent]
public sealed class UpgradePanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    public bool IsOpen => upgradePanel != null && upgradePanel.activeSelf;

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

        upgradePanel.SetActive(!upgradePanel.activeSelf);
    }
}
