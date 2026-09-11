using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UpgradeProfilePP_",
    menuName = "AllProperties/UpgradeProfileProperty")]
public class UpgradeProfileProperty : EnumStringKeyedProperty<Key_UpgradeProfilePP>
{
    [Header("Unlock")]
    [Tooltip("This upgrade becomes permanently available once ScoreManager's highest money reached meets this amount.")]
    [SerializeField] private double requiredPeakMoney;

    [Header("Levels")]
    [Tooltip("One permanent ScoreModifierProperty for each upgrade level, in purchase order.")]
    [SerializeField] private List<Key_ScoreModifierPP> levelModifiers = new();

    [Header("UI")]
    [SerializeField] private UpgradeSlotId slotId;

    public double RequiredPeakMoney => requiredPeakMoney;
    public IReadOnlyList<Key_ScoreModifierPP> LevelModifiers => levelModifiers;
    public UpgradeSlotId SlotId => slotId;
}
