using System.Linq;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Crescent.Factions;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Crescent.Factions;

/// <summary>
/// Backs the faction equipment vendor. Each configured item can be taken once per player (tracked by user id,
/// so it survives death/new bodies). The console's <see cref="AccessReaderComponent"/> restricts who may use it.
/// </summary>
public sealed class FactionEquipmentVendorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionEquipmentVendorComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FactionEquipmentVendorComponent, FactionVendorTakeMessage>(OnTake);
    }

    private void OnOpened(EntityUid uid, FactionEquipmentVendorComponent comp, BoundUIOpenedEvent args)
    {
        // The item list is the same for everyone; per-player "already taken" is enforced server-side on Take,
        // so this state stays valid even with several people using the vendor at once.
        var items = new List<FactionVendorItem>();
        foreach (var proto in comp.Items)
        {
            var name = proto.Id;
            if (_proto.TryIndex(proto, out var ent))
                name = ent.Name;

            items.Add(new FactionVendorItem { ItemId = proto, Name = name });
        }

        _ui.SetUiState(uid, FactionVendorUiKey.Key, new FactionVendorState(items));
    }

    private void OnTake(EntityUid uid, FactionEquipmentVendorComponent comp, FactionVendorTakeMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var reader) && !_accessReader.IsAllowed(actor, uid, reader))
        {
            _popup.PopupEntity(Loc.GetString("faction-vendor-denied"), uid, actor);
            return;
        }

        // Only dispense items the console actually offers.
        if (!comp.Items.Any(i => i.Id == args.ItemId))
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        var key = actorComp.PlayerSession.UserId.ToString();
        if (!comp.Claimed.TryGetValue(key, out var claimed))
        {
            claimed = new HashSet<string>();
            comp.Claimed[key] = claimed;
        }

        if (!claimed.Add(args.ItemId))
        {
            _popup.PopupEntity(Loc.GetString("faction-vendor-already-taken"), uid, actor);
            return;
        }

        var item = Spawn(args.ItemId, _xform.GetMapCoordinates(actor));
        _hands.PickupOrDrop(actor, item);
        _popup.PopupEntity(Loc.GetString("faction-vendor-dispensed"), uid, actor);
    }
}
