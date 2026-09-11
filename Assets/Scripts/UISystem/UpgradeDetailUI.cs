using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only detail panel for the currently selected upgrade.
/// It owns no upgrade state and raises a parameterless buy request to its parent UI manager.
/// </summary>
public class UpgradeDetailUI : MonoBehaviour
{
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private GameObject priceRoot;
    [SerializeField] private GameObject maxOverlay;
    [SerializeField] private Button purchaseButton;

    public event Action OnPurchaseRequested;

    private void Awake()
    {
        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(RequestPurchase);

        Clear();
    }

    private void OnDestroy()
    {
        if (purchaseButton != null)
            purchaseButton.onClick.RemoveListener(RequestPurchase);
    }

    public void Bind(UpgradeSlotSnapshot snapshot)
    {
        if (contentRoot != null)
            contentRoot.SetActive(true);

        if (nameText != null)
            nameText.text = snapshot.DisplayName;

        if (descriptionText != null)
            descriptionText.text = snapshot.Description;

        if (priceText != null)
            priceText.text = FormatMoney(snapshot.Price);

        if (priceRoot != null)
            priceRoot.SetActive(!snapshot.IsCompleted);

        if (maxOverlay != null)
            maxOverlay.SetActive(snapshot.IsCompleted);

        if (purchaseButton != null)
            purchaseButton.gameObject.SetActive(!snapshot.IsCompleted);
    }

    public void Clear()
    {
        if (contentRoot != null)
            contentRoot.SetActive(false);

        if (nameText != null)
            nameText.text = string.Empty;

        if (descriptionText != null)
            descriptionText.text = string.Empty;
    }

    private void RequestPurchase()
    {
        OnPurchaseRequested?.Invoke();
    }

    private static string FormatMoney(double value)
    {
        if (value >= 1_000_000_000d)
            return $"${value / 1_000_000_000d:0.##}B";
        if (value >= 1_000_000d)
            return $"${value / 1_000_000d:0.##}M";
        if (value >= 1_000d)
            return $"${value / 1_000d:0.##}K";

        return $"${value:0.##}";
    }
}
