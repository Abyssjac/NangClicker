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

    // Adds a direct +X Nang / Sec to automatic production. The neutral value is 0.
    AutoNangAdditiveAmt = 6,

    // Adds a direct +X Nang to one manual press. The neutral value is 0.
    ManualNangAdditiveAmt = 7,

    // Adds a direct amount to the profitable furnace range's lower bound. The neutral value is 0.
    // Upgrade data normally uses negative values so the profitable range expands downward.
    FurnaceRangeAdditiveAmt = 8,
}
