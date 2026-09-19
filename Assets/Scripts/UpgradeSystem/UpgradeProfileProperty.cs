using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "UpgradeProfilePP_",
    menuName = "AllProperties/UpgradeProfileProperty")]
public class UpgradeProfileProperty : EnumStringKeyedProperty<Key_UpgradeProfilePP>
{
    [Header("Unlock")]
    [Tooltip("This upgrade becomes permanently available once ScoreManager's cumulative earned money meets this amount.")]
    [FormerlySerializedAs("requiredPeakMoney")]
    [SerializeField] private double requiredTotalMoneyEarned;

    [Header("Levels")]
    [Tooltip("One permanent ScoreModifierProperty for each upgrade level, in purchase order.")]
    [SerializeField, PropertyDatabaseLink(typeof(ScoreModifierDatabase))]
    private List<Key_ScoreModifierPP> levelModifiers = new();

    [Header("UI")]
    [SerializeField] private UpgradeSlotId slotId;

    public double RequiredTotalMoneyEarned => requiredTotalMoneyEarned;
    public IReadOnlyList<Key_ScoreModifierPP> LevelModifiers => levelModifiers;
    public UpgradeSlotId SlotId => slotId;
}
