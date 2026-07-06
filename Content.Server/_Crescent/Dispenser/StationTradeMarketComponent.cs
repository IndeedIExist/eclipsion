namespace Content.Server.Crescent.Dispenser;

[RegisterComponent]
public sealed partial class StationTradeMarketComponent : Component
{
    [DataField]
    public Dictionary<string, float> SalesAccumulator = new();

    [DataField]
    public float PriceDropPerSale = 0.02f;

    [DataField]
    public float MinMultiplier = 0.3f;


    [DataField]
    public float RecoveryRatePerSecond = 1f / 60f;

    // --- Taxation ---------------------------------------------------------

    /// <summary>
    /// Station-wide default tax rate (0..1) applied to every trade good sold through
    /// this station's trade points, unless a per-good override exists in <see cref="TaxOverrides"/>.
    /// Set from the taxation console.
    /// </summary>
    [DataField]
    public float DefaultTaxRate = 0f;

    /// <summary>
    /// Per-trade-good tax rate overrides (0..1), keyed by trade good prototype id.
    /// Takes precedence over <see cref="DefaultTaxRate"/> for that specific good.
    /// </summary>
    [DataField]
    public Dictionary<string, float> TaxOverrides = new();

    /// <summary>
    /// Hard ceiling on any tax rate so a console operator can never confiscate the
    /// entire payout (which would make trading pointless).
    /// </summary>
    [DataField]
    public float MaxTaxRate = 0.95f;

    /// <summary>
    /// Accumulated tax revenue held by the faction, withdrawable at the treasury console.
    /// </summary>
    [DataField]
    public int TreasuryBalance = 0;
}
