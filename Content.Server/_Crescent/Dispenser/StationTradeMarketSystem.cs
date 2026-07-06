using Content.Server.Station.Events;  
using Content.Server.Station.Systems;  
using JetBrains.Annotations;  
  
namespace Content.Server.Crescent.Dispenser;

[UsedImplicitly]  
public sealed class StationTradeMarketSystem : EntitySystem  
{  
    [Dependency] private readonly StationSystem _station = default!;  
  
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
    /// Adds tax revenue to the faction treasury. Returns the new balance.
    /// </summary>
    public int AddTreasury(EntityUid stationUid, int amount)
    {
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        market.TreasuryBalance += amount;
        return market.TreasuryBalance;
    }

    public int GetTreasury(EntityUid stationUid)
    {
        return TryComp<StationTradeMarketComponent>(stationUid, out var market) ? market.TreasuryBalance : 0;
    }

    /// <summary>
    /// Removes up to <paramref name="amount"/> from the treasury, clamped to the available
    /// balance. Returns the amount actually removed.
    /// </summary>
    public int TryWithdrawTreasury(EntityUid stationUid, int amount)
    {
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        var taken = Math.Min(amount, market.TreasuryBalance);
        market.TreasuryBalance -= taken;
        return taken;
    }
}