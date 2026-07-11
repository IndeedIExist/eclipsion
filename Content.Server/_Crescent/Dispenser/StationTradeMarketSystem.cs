using Content.Server._Crescent.Taxation;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using JetBrains.Annotations;
using Robust.Shared.Network;
  
namespace Content.Server.Crescent.Dispenser;

[UsedImplicitly]  
public sealed class StationTradeMarketSystem : EntitySystem  
{  
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly FactionTreasurySystem _treasury = default!;

    public override void Initialize()  
    {
        base.Initialize();  
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);  
    }
  
    private void OnStationPostInit(ref StationPostInitEvent ev)  
    {
        EnsureComp<StationTradeMarketComponent>(ev.Station);  
    }
  
    public override void Update(float frameTime)  
    {
        base.Update(frameTime);  
  
        var query = EntityQueryEnumerator<StationTradeMarketComponent>();
        while (query.MoveNext(out _, out var market))
        {
            if (market.SalesAccumulator.Count == 0)
                continue;
  
            var toRemove = new List<string>();  
            foreach (var (goodId, accumulated) in market.SalesAccumulator)
            {  
                var newValue = accumulated - market.RecoveryRatePerSecond * frameTime;
                if (newValue <= 0f)  
                    toRemove.Add(goodId);  
                else  
                    market.SalesAccumulator[goodId] = newValue;  
            }  
  
            foreach (var key in toRemove)  
                market.SalesAccumulator.Remove(key);  
        }  
    }
	
    public float GetPriceMultiplier(EntityUid stationUid, string tradeGoodId)  
    {  
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))  
            return 1.0f;  
  
        if (!market.SalesAccumulator.TryGetValue(tradeGoodId, out var accumulated))  
            return 1.0f;  
  
        return MathF.Max(market.MinMultiplier, 1.0f - accumulated * market.PriceDropPerSale);  
    }  

    public void RecordSale(EntityUid stationUid, string tradeGoodId)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return;

        market.SalesAccumulator.TryGetValue(tradeGoodId, out var current);
        market.SalesAccumulator[tradeGoodId] = current + 1.0f;
    }

    public EntityUid? TryGetOwningStation(EntityUid entityUid)
    {
        return _station.GetOwningStation(entityUid);
    }

    // --- Taxation ---------------------------------------------------------

    /// <summary>
    /// Resolves the effective tax rate (0..MaxTaxRate) for a trade good on this station:
    /// a per-good override if present, otherwise the station-wide default.
    /// </summary>
    public float GetTaxRate(EntityUid stationUid, string tradeGoodId)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0f;

        var rate = market.TaxOverrides.TryGetValue(tradeGoodId, out var over)
            ? over
            : market.DefaultTaxRate;

        return Math.Clamp(rate, 0f, market.MaxTaxRate);
    }

    public void SetDefaultTaxRate(EntityUid stationUid, float rate)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return;

        market.DefaultTaxRate = Math.Clamp(rate, 0f, market.MaxTaxRate);
    }

    public void SetTaxOverride(EntityUid stationUid, string tradeGoodId, float rate)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return;

        market.TaxOverrides[tradeGoodId] = Math.Clamp(rate, 0f, market.MaxTaxRate);
    }

    public void ClearTaxOverride(EntityUid stationUid, string tradeGoodId)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return;

        market.TaxOverrides.Remove(tradeGoodId);
    }

    /// <summary>
    /// Binds a station's treasury to a faction and loads that faction's persisted, cross-round balance
    /// into it (once per round). Called when a faction treasury console initializes on the station.
    /// </summary>
    public void BindFactionTreasury(EntityUid stationUid, string faction)
    {
        if (string.IsNullOrEmpty(faction))
            return;

        var market = EnsureComp<StationTradeMarketComponent>(stationUid);

        // Only the first console to bind this round establishes the faction and loads the balance;
        // re-binding would clobber tax accrued after load.
        if (market.TreasuryLoaded)
            return;

        market.Faction = faction;
        market.TreasuryBalance = _treasury.Get(faction);
        market.TreasuryLoaded = true;
    }

    /// <summary>Mirrors a station's current balance into the cross-round faction store.</summary>
    private void PersistTreasury(StationTradeMarketComponent market)
    {
        if (!string.IsNullOrEmpty(market.Faction))
            _treasury.Set(market.Faction, market.TreasuryBalance);
    }

    /// <summary>
    /// Adds tax revenue to the faction treasury. Returns the new balance.
    /// </summary>
    public int AddTreasury(EntityUid stationUid, int amount)
    {
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        market.TreasuryBalance += amount;
        PersistTreasury(market);
        return market.TreasuryBalance;
    }

    public int GetTreasury(EntityUid stationUid)
    {
        return TryComp<StationTradeMarketComponent>(stationUid, out var market) ? market.TreasuryBalance : 0;
    }

    /// <summary>Overwrites a station's treasury balance (admin) and persists it. Returns the new balance.</summary>
    public int SetTreasury(EntityUid stationUid, int value)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        market.TreasuryBalance = Math.Max(0, value);
        PersistTreasury(market);
        return market.TreasuryBalance;
    }

    /// <summary>
    /// Removes up to <paramref name="amount"/> from the treasury, clamped to the available
    /// balance. Returns the amount actually removed. Uncapped — used for robbery/looting.
    /// </summary>
    public int TryWithdrawTreasury(EntityUid stationUid, int amount)
    {
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        var taken = Math.Min(amount, market.TreasuryBalance);
        market.TreasuryBalance -= taken;
        PersistTreasury(market);
        return taken;
    }

    /// <summary>
    /// Withdraws cash for a specific player, enforcing a per-player per-round cap of
    /// <paramref name="maxFraction"/> of the treasury. The cap is measured against the vault as it
    /// stood before this player started withdrawing (current balance + their prior withdrawals), so a
    /// member can never exceed that share no matter how many times they come back. Returns the amount
    /// actually withdrawn.
    /// </summary>
    public int TryWithdrawTreasuryCapped(EntityUid stationUid, NetUserId user, int amount, float maxFraction)
    {
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market) || market.TreasuryBalance <= 0)
            return 0;

        market.WithdrawnThisRound.TryGetValue(user, out var already);

        var reference = market.TreasuryBalance + already;
        var cap = (int) (reference * Math.Clamp(maxFraction, 0f, 1f));
        var remaining = Math.Max(0, cap - already);
        if (remaining <= 0)
            return 0;

        var taken = Math.Min(Math.Min(amount, remaining), market.TreasuryBalance);
        if (taken <= 0)
            return 0;

        market.TreasuryBalance -= taken;
        market.WithdrawnThisRound[user] = already + taken;
        PersistTreasury(market);
        return taken;
    }
}