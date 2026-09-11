using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreModifierPP_",
    menuName = "AllProperties/ScoreModifierProperty")]
public class ScoreModifierProperty : EnumStringKeyedProperty<Key_ScoreModifierPP>
{
    [Header("Formula")]
    [SerializeField] private ScoreModifierType modifierType;

    [Tooltip("For additive rates, use 0.1 for +10%. For multipliers, use 1.1 for x1.1. Flat bonuses use direct money values.")]
    [SerializeField] private double amount;

    [Header("Lifetime")]
    [Tooltip("0 means this is a permanent Property. A positive value makes it temporary. Reapplying the same temporary Property extends time but does not stack its amount.")]
    [SerializeField, Min(0f)] private float defaultDuration;

    [Header("UpgradePrice")]
    [SerializeField] private double upgradePrice;

    [Header("UI Display")]
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 5)] private string description;

    public ScoreModifierType ModifierType => modifierType;
    public double Amount => amount;
    public float DefaultDuration => defaultDuration;
    public bool IsTemporary => defaultDuration > 0f;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public double UpgradePrice => upgradePrice;
}
