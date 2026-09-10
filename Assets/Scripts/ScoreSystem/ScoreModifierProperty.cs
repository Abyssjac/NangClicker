using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreModifierProperty_",
    menuName = "Nang Clicker/Score System/Score Modifier Property")]
public class ScoreModifierProperty : EnumStringKeyedProperty<ScoreModifierPropertyId>
{
    [Header("Formula")]
    [SerializeField] private ScoreModifierType modifierType;

    [Tooltip("For additive rates, use 0.1 for +10%. For multipliers, use 1.1 for x1.1. Flat bonuses use direct money values.")]
    [SerializeField] private double amount;

    [Header("Lifetime")]
    [Tooltip("0 means this is a permanent Property. A positive value makes it temporary. Reapplying the same temporary Property extends time but does not stack its amount.")]
    [SerializeField, Min(0f)] private float defaultDuration;

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
}
