using System.Linq;
using Content.Server.Crescent.Dispenser;
using Content.Shared._Crescent.Taxation;
using Content.Shared.Access.Systems;
using Content.Shared.Crescent.Dispenser;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// Backs the taxation console: lets an authorized faction member set the percentage tax
/// applied to trade goods sold through the station's trade points. Base prices are never
/// touched here — only the tax cut, stored on the station's <see cref="StationTradeMarketComponent"/>.
/// </summary>
public sealed class TaxationConsoleSystem : EntitySystem
{
    [Dependency] private readonly StationTradeMarketSystem _market = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TaxationConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<TaxationConsoleComponent, TaxationSetDefaultRateMessage>(OnSetDefault);
        SubscribeLocalEvent<TaxationConsoleComponent, TaxationSetOverrideMessage>(OnSetOverride);
        SubscribeLocalEvent<TaxationConsoleComponent, TaxationClearOverrideMessage>(OnClearOverride);
    }

    private void OnOpened(EntityUid uid, TaxationConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, args.Actor);
    }

    private void OnSetDefault(EntityUid uid, TaxationConsoleComponent comp, TaxationSetDefaultRateMessage args)
    {
        if (!TryEdit(uid, comp, args.Actor, out var station))
            return;

        _market.SetDefaultTaxRate(station, args.Rate);
        UpdateUi(uid, args.Actor);
    }

    private void OnSetOverride(EntityUid uid, TaxationConsoleComponent comp, TaxationSetOverrideMessage args)
    {
        if (!TryEdit(uid, comp, args.Actor, out var station))
            return;

        _market.SetTaxOverride(station, args.ProtoId, args.Rate);
        UpdateUi(uid, args.Actor);
    }

    private void OnClearOverride(EntityUid uid, TaxationConsoleComponent comp, TaxationClearOverrideMessage args)
    {
        if (!TryEdit(uid, comp, args.Actor, out var station))
            return;

        _market.ClearTaxOverride(station, args.ProtoId);
        UpdateUi(uid, args.Actor);
    }

    /// <summary>Validates edit access and resolves the owning station.</summary>
    private bool TryEdit(EntityUid uid, TaxationConsoleComponent comp, EntityUid actor, out EntityUid station)
    {
        station = default;

        if (!_access.IsAllowed(actor, uid))
        {
            _popup.PopupEntity(Loc.GetString("taxation-console-access-denied"), uid, actor, PopupType.MediumCaution);
            return false;
        }

        var owning = _market.TryGetOwningStation(uid);
        if (owning is null)
            return false;

        station = owning.Value;
        return true;
    }

    private void UpdateUi(EntityUid uid, EntityUid actor)
    {
        var station = _market.TryGetOwningStation(uid);
        if (station is null || !TryComp<StationTradeMarketComponent>(station, out var market))
            return;

        var canEdit = _access.IsAllowed(actor, uid);
        var goods = BuildGoodsList(station.Value, market);

        var state = new TaxationConsoleState(
            market.DefaultTaxRate,
            market.MaxTaxRate,
            market.TreasuryBalance,
            canEdit,
            goods);

        _ui.SetUiState(uid, TaxationConsoleUiKey.Key, state);
    }

    /// <summary>
    /// Collects every trade good sold through any trade point (dispenser) on this station,
    /// with its base price and the tax rate currently in effect.
    /// </summary>
    private List<TaxableGoodEntry> BuildGoodsList(EntityUid station, StationTradeMarketComponent market)
    {
        // Union the base prices from every dispenser belonging to this station.
        var basePrices = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<DispenserComponent>();
        while (query.MoveNext(out var dispenserUid, out var dispenser))
        {
            if (dispenser.DynamicInventory.Count == 0)
                continue;

            if (_market.TryGetOwningStation(dispenserUid) != station)
                continue;

            foreach (var (protoId, price) in dispenser.DynamicInventory)
            {
                // Keep the highest listed base price if multiple trade points differ.
                if (!basePrices.TryGetValue(protoId, out var existing) || price > existing)
                    basePrices[protoId] = price;
            }
        }

        var result = new List<TaxableGoodEntry>(basePrices.Count);
        foreach (var (protoId, price) in basePrices)
        {
            var name = _proto.TryIndex<EntityPrototype>(protoId, out var proto) ? proto.Name : protoId;
            var hasOverride = market.TaxOverrides.ContainsKey(protoId);

            result.Add(new TaxableGoodEntry
            {
                ProtoId = protoId,
                Name = name,
                BasePrice = price,
                EffectiveRate = _market.GetTaxRate(station, protoId),
                HasOverride = hasOverride,
            });
        }

        return result.OrderByDescending(g => g.BasePrice).ToList();
    }
}
