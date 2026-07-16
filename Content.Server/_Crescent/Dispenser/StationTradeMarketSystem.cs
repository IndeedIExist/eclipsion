using Content.Server._Crescent.Taxation;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared.Shuttles.Components;
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
        while (query.MoveNext(out var uid, out var market))
        {
            // Resolve each station's faction (and load its cross-round balance) as soon as it exists,
            // so the treasury accrues and persists without any console ever being placed. Cheap after
            // the first success — TreasuryLoaded short-circuits it.
            EnsureFactionLoaded(uid, market);

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
    /// into it (once per round). Normally the faction is resolved automatically from the station's IFF
    /// (see <see cref="EnsureFactionLoaded"/>); this lets a console force a faction on a station whose
    /// grid carries no IFF faction of its own.
    /// </summary>
    public void BindFactionTreasury(EntityUid stationUid, string faction)
    {
        if (string.IsNullOrEmpty(faction))
            return;

        var market = EnsureComp<StationTradeMarketComponent>(stationUid);

        // First writer wins for the round; re-binding would clobber tax accrued after load.
        if (market.TreasuryLoaded)
            return;

        market.Faction = faction;
        market.TreasuryBalance = _treasury.Get(faction);
        market.TreasuryLoaded = true;
    }

    /// <summary>
    /// Ensures the station's treasury is bound to its faction and its cross-round balance loaded, so
    /// the vault exists and accrues whether or not any treasury console is ever placed. The faction is
    /// taken from the station grid's IFF faction; stations with no faction ("Neutral") stay per-round.
    /// Runs once per round per station — <see cref="StationTradeMarketComponent.TreasuryLoaded"/> guards it.
    /// </summary>
    private void EnsureFactionLoaded(EntityUid stationUid, StationTradeMarketComponent market)
    {
        if (market.TreasuryLoaded)
            return;

        var faction = ResolveStationFaction(stationUid);
        if (string.IsNullOrEmpty(faction))
            return;

        market.Faction = faction;
        market.TreasuryBalance = _treasury.Get(faction);
        market.TreasuryLoaded = true;
    }

    /// <summary>
    /// The faction a station belongs to, read from its grids' IFF faction (set from the game map on
    /// spawn). Returns empty for unaligned stations. Only set after the station's grids exist, which is
    /// why loading is deferred to <see cref="Update"/> rather than done at station post-init.
    /// </summary>
    private string ResolveStationFaction(EntityUid stationUid)
    {
        if (!TryComp<StationDataComponent>(stationUid, out var data))
            return string.Empty;

        foreach (var gridUid in data.Grids)
        {
            if (TryComp<IFFComponent>(gridUid, out var iff)
                && !string.IsNullOrEmpty(iff.Faction)
                && iff.Faction != "Neutral")
            {
                return iff.Faction;
            }
        }

        return string.Empty;
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

        // Bind + load before adding, so tax landing before the first Update tick tops up the persisted
        // balance rather than being overwritten by a later load.
        EnsureFactionLoaded(stationUid, market);
        market.TreasuryBalance += amount;
        PersistTreasury(market);
        return market.TreasuryBalance;
    }

    public int GetTreasury(EntityUid stationUid)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        EnsureFactionLoaded(stationUid, market);
        return market.TreasuryBalance;
    }

    /// <summary>Overwrites a station's treasury balance (admin) and persists it. Returns the new balance.</summary>
    public int SetTreasury(EntityUid stationUid, int value)
    {
        if (!TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        EnsureFactionLoaded(stationUid, market);
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

        EnsureFactionLoaded(stationUid, market);
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
        if (amount <= 0 || !TryComp<StationTradeMarketComponent>(stationUid, out var market))
            return 0;

        EnsureFactionLoaded(stationUid, market);
        if (market.TreasuryBalance <= 0)
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

    /// <summary>
    /// Finds the station holding a faction's treasury this round, regardless of where the caller is.
    /// Callers that are faction-scoped rather than station-scoped must use this instead of
    /// <c>GetOwningStation</c>, which would resolve to whichever station they happen to sit on.
    /// </summary>
    public EntityUid? TryGetFactionTreasuryStation(string faction)
    {
        if (string.IsNullOrEmpty(faction))
            return null;

        var query = EntityQueryEnumerator<StationTradeMarketComponent>();
        while (query.MoveNext(out var uid, out var market))
        {
            // Bind lazily here too, so a faction's own station is found on the very first frame even
            // before Update has run — e.g. payroll paying out immediately at round start.
            EnsureFactionLoaded(uid, market);

            if (market.TreasuryLoaded && market.Faction == faction)
                return uid;
        }

        return null;
    }
}