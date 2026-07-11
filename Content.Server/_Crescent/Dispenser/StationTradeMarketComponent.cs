using Robust.Shared.Network;

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

    /// <summary>
    /// Faction key this station's treasury belongs to (e.g. "SHI", "NCWL", "DSM"), set from the
    /// faction treasury console(s) present on the station. While non-empty the balance is persisted
    /// across rounds under this key; empty means the treasury is per-round only.
    /// </summary>
    [ViewVariables]
    public string Faction = string.Empty;

    /// <summary>
    /// Whether this round's balance has already been loaded from the cross-round store. Guards against
    /// re-loading (and clobbering accrued tax) when multiple consoles bind the same station.
    /// </summary>
    [ViewVariables]
    public bool TreasuryLoaded = false;

    /// <summary>
    /// Per-player cumulative treasury withdrawals this round, keyed by player user id. Backs the
    /// per-person withdrawal cap so no single member can drain the vault. Reset each round because
    /// this component is recreated when the station initializes.
    /// </summary>
    [ViewVariables]
    public Dictionary<NetUserId, int> WithdrawnThisRound = new();
}
