using UnityEngine;

/// <summary>
/// A display-only view of one unlocked upgrade. The UI must send only ProfileId back to UpgradeManager.
/// </summary>
public readonly struct UpgradeSlotSnapshot
{
    public UpgradeSlotId SlotId { get; }
    public Key_UpgradeProfilePP ProfileId { get; }
    public Sprite Icon { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public double Price { get; }
    public int CurrentLevel { get; }
    public int MaxLevel { get; }
    public bool IsCompleted { get; }

    public UpgradeSlotSnapshot(
        UpgradeSlotId slotId,
        Key_UpgradeProfilePP profileId,
        Sprite icon,
        string displayName,
        string description,
        double price,
        int currentLevel,
        int maxLevel,
        bool isCompleted)
    {
        SlotId = slotId;
        ProfileId = profileId;
        Icon = icon;
        DisplayName = displayName;
        Description = description;
        Price = price;
        CurrentLevel = currentLevel;
        MaxLevel = maxLevel;
        IsCompleted = isCompleted;
    }
}
