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
}