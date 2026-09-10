public enum ScoreModifierType
{
    None = 0,

    // a: Sum all rate contributions, then use (1 + a).
    UnitSalePriceAdditiveRate = 1,

    // b: Multiply all contributions. The neutral value is 1.
    UnitSalePriceMultiplier = 2,

    // c: Sum all flat money contributions after the unit-price multiplier.
    UnitSalePriceFlatBonus = 3,

    // d: Sum all rate contributions, then use (1 + d).
    IncomeAdditiveRate = 4,

    // e: Multiply all contributions. The neutral value is 1.
    IncomeMultiplier = 5,
}
