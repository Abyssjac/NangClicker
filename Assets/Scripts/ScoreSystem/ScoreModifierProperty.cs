using System;
using System.Globalization;
using JackyUtility;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreModifierPP_",
    menuName = "AllProperties/ScoreModifierProperty")]
public class ScoreModifierProperty : EnumStringKeyedProperty<Key_ScoreModifierPP>
{
    [Header("Formula")]
    [SerializeField] private ScoreModifierType modifierType;

    [Tooltip("For additive rates, use 0.1 for +10%. For multipliers, use 1.1 for x1.1. Unit-price flat bonuses use direct money values. Auto Nang Additive Amount uses direct +X Nang / Sec values.")]
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
    public string EffectText => FormatEffectText(modifierType, amount);

    private static string FormatEffectText(ScoreModifierType type, double value)
    {
        switch (type)
        {
            case ScoreModifierType.UnitSalePriceAdditiveRate:
                return $"Unit Sale Price {FormatSigned(value * 100d)}%";
            case ScoreModifierType.UnitSalePriceMultiplier:
                return $"Unit Sale Price ×{FormatNumber(value)}";
            case ScoreModifierType.UnitSalePriceFlatBonus:
                return $"Unit Sale Price {FormatSignedMoney(value)}";
            case ScoreModifierType.IncomeAdditiveRate:
                return $"Income {FormatSigned(value * 100d)}%";
            case ScoreModifierType.IncomeMultiplier:
                return $"Income ×{FormatNumber(value)}";
            case ScoreModifierType.AutoNangAdditiveAmt:
                return $"Automatic Production {FormatSigned(value)} Nang/Sec";
            case ScoreModifierType.ManualNangAdditiveAmt:
                return $"Manual Press {FormatSigned(value)} Nang";
            case ScoreModifierType.FurnaceRangeAdditiveAmt:
                return FormatFurnaceRangeEffect(value);
            default:
                return string.Empty;
        }
    }

    private static string FormatFurnaceRangeEffect(double value)
    {
        if (value < 0d)
            return $"Profitable Heat Range expands downward by {FormatNumber(Math.Abs(value))}";

        if (value > 0d)
            return $"Profitable Heat Range shifts upward by {FormatNumber(value)}";

        return "Profitable Heat Range unchanged";
    }

    private static string FormatSigned(double value)
    {
        return value >= 0d ? $"+{FormatNumber(value)}" : FormatNumber(value);
    }

    private static string FormatSignedMoney(double value)
    {
        string amountText = Math.Abs(value).ToString("#,0.##", CultureInfo.InvariantCulture);
        return value >= 0d ? $"+${amountText}" : $"-${amountText}";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
