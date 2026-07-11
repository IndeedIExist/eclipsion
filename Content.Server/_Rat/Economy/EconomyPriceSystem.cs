using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Bank;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Crescent.Dispenser;
using Content.Shared.Bank.Components;
using Content.Shared._NF.Cargo.Components;
using Content.Shared._Rat.Economy;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Shipyard.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Rat.Economy;

/// <summary>
/// Runtime economy price overrides editable from the admin menu.
/// </summary>
public sealed class EconomyPriceSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly StationTradeMarketSystem _market = default!;

    private readonly Dictionary<string, double> _itemOverrides = new();
    private readonly Dictionary<string, int> _vesselOverrides = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<EconomyAdminRequestListEvent>(OnRequestList);
        SubscribeNetworkEvent<EconomyAdminSetPriceEvent>(OnSetPrice);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Connected)
            return;

        RaiseNetworkEvent(new EconomyPriceSyncEvent
        {
            ItemOverrides = new Dictionary<string, double>(_itemOverrides),
            VesselOverrides = new Dictionary<string, int>(_vesselOverrides),
        }, args.Session);
    }

    public bool TryGetItemOverride(string protoId, out double price) =>
        _itemOverrides.TryGetValue(protoId, out price);

    public int GetVesselPrice(VesselPrototype vessel) =>
        _vesselOverrides.TryGetValue(vessel.ID, out var price) ? price : vessel.Price;

    public double GetEffectiveItemPrice(string protoId, double basePrice) =>
        _itemOverrides.TryGetValue(protoId, out var price) ? price : basePrice;

    private void OnRequestList(EconomyAdminRequestListEvent msg, EntitySessionEventArgs args)
    {
        if (!IsEconomyAdmin(args.SenderSession))
            return;

        var entries = msg.Category switch
        {
            EconomyListCategory.Items => BuildItemEntries(msg.SearchFilter),
            EconomyListCategory.Vessels => BuildVesselEntries(msg.SearchFilter),
            EconomyListCategory.Treasury => BuildTreasuryEntries(msg.SearchFilter),
            EconomyListCategory.Players => BuildPlayerEntries(msg.SearchFilter),
            _ => new List<EconomyPriceEntry>(),
        };

        RaiseNetworkEvent(new EconomyAdminListEvent
        {
            Category = msg.Category,
            Entries = entries,
        }, args.SenderSession);
    }

    private void OnSetPrice(EconomyAdminSetPriceEvent msg, EntitySessionEventArgs args)
    {
        if (!IsEconomyAdmin(args.SenderSession))
            return;

        if (msg.Price < 0)
            return;

        // Treasury and player-bank balances are live entity state, not prototype price overrides.
        switch (msg.Category)
        {
            case EconomyListCategory.Treasury:
                SetTreasuryBalance(msg, args.SenderSession);
                return;
            case EconomyListCategory.Players:
                SetPlayerBalance(msg, args.SenderSession);
                return;
        }

        double oldPrice;
        double basePrice;
        var resetToBase = false;

        switch (msg.Category)
        {
            case EconomyListCategory.Items:
                if (!_prototypeManager.TryIndex<EntityPrototype>(msg.Id, out var proto)
                    || !TryGetItemPriceInfo(proto, out _, out basePrice))
                {
                    return;
                }

                oldPrice = GetEffectiveItemPrice(msg.Id, basePrice);
                resetToBase = Math.Abs(msg.Price - basePrice) < 0.001;

                if (resetToBase)
                    _itemOverrides.Remove(msg.Id);
                else
                    _itemOverrides[msg.Id] = msg.Price;

                ApplyItemPriceToWorld(msg.Id, msg.Price);
                break;
            case EconomyListCategory.Vessels:
                if (!_prototypeManager.TryIndex<VesselPrototype>(msg.Id, out var vessel))
                    return;

                basePrice = vessel.Price;
                oldPrice = GetVesselPrice(vessel);
                resetToBase = (int) Math.Round(msg.Price) == vessel.Price;

                _vesselOverrides[msg.Id] = (int) Math.Round(msg.Price);
                if (resetToBase)
                    _vesselOverrides.Remove(msg.Id);
                break;
            default:
                return;
        }

        var category = msg.Category == EconomyListCategory.Items ? "item" : "vessel";
        var newPrice = resetToBase ? basePrice : msg.Price;

        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{args.SenderSession:player} changed economy {category} price for {msg.Id} from {oldPrice:0.##} to {newPrice:0.##} (base {basePrice:0.##})");

        BroadcastPriceSync();
        RaiseNetworkEvent(new EconomyAdminPriceUpdatedEvent
        {
            Category = msg.Category,
            Id = msg.Id,
            Price = newPrice,
        });
    }

    private void BroadcastPriceSync()
    {
        var sync = new EconomyPriceSyncEvent
        {
            ItemOverrides = new Dictionary<string, double>(_itemOverrides),
            VesselOverrides = new Dictionary<string, int>(_vesselOverrides),
        };

        RaiseNetworkEvent(sync);
    }

    private void ApplyItemPriceToWorld(string protoId, double price)
    {
        var staticQuery = EntityQueryEnumerator<StaticPriceComponent, MetaDataComponent>();
        while (staticQuery.MoveNext(out var uid, out var comp, out var meta))
        {
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            comp.Price = price;
            Dirty(uid, comp);
        }

        var stackQuery = EntityQueryEnumerator<StackPriceComponent, MetaDataComponent>();
        while (stackQuery.MoveNext(out var uid, out var comp, out var meta))
        {
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            comp.Price = price;
            Dirty(uid, comp);
        }

        var vendQuery = EntityQueryEnumerator<VendPriceComponent, MetaDataComponent>();
        while (vendQuery.MoveNext(out var uid, out var comp, out var meta))
        {
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            comp.Price = price;
            Dirty(uid, comp);
        }

        var mobQuery = EntityQueryEnumerator<MobPriceComponent, MetaDataComponent>();
        while (mobQuery.MoveNext(out var uid, out var comp, out var meta))
        {
            if (meta.EntityPrototype?.ID != protoId)
                continue;

            comp.Price = price;
            Dirty(uid, comp);
        }
    }

    private List<EconomyPriceEntry> BuildItemEntries(string searchFilter)
    {
        var filter = searchFilter.Trim();
        var entries = new List<EconomyPriceEntry>();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (!TryGetItemPriceInfo(proto, out var kind, out var basePrice))
                continue;

            var name = proto.Name;
            if (filter.Length > 0
                && !proto.ID.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var current = GetEffectiveItemPrice(proto.ID, basePrice);
            entries.Add(new EconomyPriceEntry(
                proto.ID,
                name,
                EconomyListCategory.Items,
                kind,
                basePrice,
                current));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return entries;
    }

    private List<EconomyPriceEntry> BuildVesselEntries(string searchFilter)
    {
        var filter = searchFilter.Trim();
        var entries = new List<EconomyPriceEntry>();

        foreach (var vessel in _prototypeManager.EnumeratePrototypes<VesselPrototype>())
        {
            if (filter.Length > 0
                && !vessel.ID.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !vessel.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var current = GetVesselPrice(vessel);
            entries.Add(new EconomyPriceEntry(
                vessel.ID,
                vessel.Name,
                EconomyListCategory.Vessels,
                null,
                vessel.Price,
                current));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return entries;
    }

    private bool TryGetItemPriceInfo(EntityPrototype proto, out EconomyPriceKind kind, out double basePrice)
    {
        if (proto.Components.TryGetValue(_factory.GetComponentName(typeof(StaticPriceComponent)), out var staticProto))
        {
            kind = EconomyPriceKind.Static;
            basePrice = ((StaticPriceComponent) staticProto.Component).Price;
            return true;
        }

        if (proto.Components.TryGetValue(_factory.GetComponentName(typeof(StackPriceComponent)), out var stackProto))
        {
            kind = EconomyPriceKind.Stack;
            basePrice = ((StackPriceComponent) stackProto.Component).Price;
            return true;
        }

        if (proto.Components.TryGetValue(_factory.GetComponentName(typeof(VendPriceComponent)), out var vendProto))
        {
            kind = EconomyPriceKind.Vend;
            basePrice = ((VendPriceComponent) vendProto.Component).Price;
            return true;
        }

        if (proto.Components.TryGetValue(_factory.GetComponentName(typeof(MobPriceComponent)), out var mobProto))
        {
            kind = EconomyPriceKind.Mob;
            basePrice = ((MobPriceComponent) mobProto.Component).Price;
            return true;
        }

        kind = default;
        basePrice = 0;
        return false;
    }

    private List<EconomyPriceEntry> BuildTreasuryEntries(string searchFilter)
    {
        var filter = searchFilter.Trim();
        var entries = new List<EconomyPriceEntry>();

        var query = EntityQueryEnumerator<StationTradeMarketComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var market, out var meta))
        {
            var name = meta.EntityName;
            var id = uid.Id.ToString();

            if (filter.Length > 0
                && !id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(new EconomyPriceEntry(
                id,
                name,
                EconomyListCategory.Treasury,
                null,
                market.TreasuryBalance,
                market.TreasuryBalance));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return entries;
    }

    private List<EconomyPriceEntry> BuildPlayerEntries(string searchFilter)
    {
        var filter = searchFilter.Trim();
        var entries = new List<EconomyPriceEntry>();

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.Connected)
                continue;

            if (session.AttachedEntity is not { } mob || !TryComp<BankAccountComponent>(mob, out var bank))
                continue;

            var charName = Comp<MetaDataComponent>(mob).EntityName;
            var name = $"{charName} [{session.Name}]";
            var id = session.UserId.ToString();

            if (filter.Length > 0
                && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                && !session.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(new EconomyPriceEntry(
                id,
                name,
                EconomyListCategory.Players,
                null,
                bank.Balance,
                bank.Balance));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return entries;
    }

    private void SetTreasuryBalance(EconomyAdminSetPriceEvent msg, ICommonSession session)
    {
        var newBalance = (int) Math.Round(msg.Price);

        var query = EntityQueryEnumerator<StationTradeMarketComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var market, out var meta))
        {
            if (uid.Id.ToString() != msg.Id)
                continue;

            var oldBalance = market.TreasuryBalance;
            // Route through the market system so the change is mirrored into the cross-round store.
            _market.SetTreasury(uid, newBalance);

            _adminLog.Add(
                LogType.AdminCommands,
                LogImpact.High,
                $"{session:player} set {meta.EntityName} faction treasury from {oldBalance} to {newBalance} cr");

            RaiseNetworkEvent(new EconomyAdminPriceUpdatedEvent
            {
                Category = EconomyListCategory.Treasury,
                Id = msg.Id,
                Price = newBalance,
            });
            return;
        }
    }

    private void SetPlayerBalance(EconomyAdminSetPriceEvent msg, ICommonSession session)
    {
        var newBalance = (long) Math.Round(msg.Price);

        foreach (var target in _playerManager.Sessions)
        {
            if (target.UserId.ToString() != msg.Id)
                continue;

            if (target.AttachedEntity is not { } mob || !TryComp<BankAccountComponent>(mob, out var bank))
                return;

            var oldBalance = bank.Balance;
            if (!_bank.TrySetBankBalance(mob, newBalance))
                return;

            _adminLog.Add(
                LogType.AdminCommands,
                LogImpact.High,
                $"{session:player} set {target.Name}'s bank balance from {oldBalance} to {newBalance} cr");

            RaiseNetworkEvent(new EconomyAdminPriceUpdatedEvent
            {
                Category = EconomyListCategory.Players,
                Id = msg.Id,
                Price = newBalance,
            });
            return;
        }
    }

    private bool IsEconomyAdmin(ICommonSession session) =>
        _adminManager.HasAdminFlag(session, AdminFlags.Admin);
}
