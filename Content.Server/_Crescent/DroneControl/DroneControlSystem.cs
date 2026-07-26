using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Crescent.DroneControl;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.DroneControl;

public sealed class DroneControlSystem : EntitySystem
{
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;

    private EntityQuery<DroneControlComponent> _controlQuery;

    private HashSet<Entity<DockingComponent>> _docks = new();
    private HashSet<Entity<DroneControlComponent>> _controllers = new();

    public override void Initialize()
    {
        base.Initialize();

        // Manual autolink is intentionally disabled: a carrier only fields the drones it produces, so players
        // can't wire extra drones in with a multitool. Deployment/linking is handled by AutoDroneSystem.
        // SubscribeLocalEvent<DroneControlConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);

        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleMoveMessage>(OnMoveMsg);
        SubscribeLocalEvent<DroneControlConsoleComponent, DroneConsoleTargetMessage>(OnTargetMsg);

        SubscribeLocalEvent<DroneControlComponent, DeviceNetworkPacketEvent>(OnPacketReceived);

        _controlQuery = GetEntityQuery<DroneControlComponent>();
    }

    private void OnGetAltVerbs(Entity<DroneControlConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("drone-control-autolink"),
            Priority = 10,
            Act = () => TryAutolink(ent)
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DroneControlConsoleComponent, DeviceListComponent>();
        while (query.MoveNext(out var uid, out var comp, out var devList))
        {
             if (_ui.IsUiOpen(uid, DroneConsoleUiKey.Key))
             {
                 UpdateState(uid);
             }
        }
    }

    private void OnMoveMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleMoveMessage args)
    {
        DoTargetedDroneOrder(ent, args.SelectedDrones, DroneOrderType.Move, GetCoordinates(args.TargetCoordinates), args.Actor);
    }

    private void OnTargetMsg(Entity<DroneControlConsoleComponent> ent, ref DroneConsoleTargetMessage args)
    {
        DoTargetedDroneOrder(ent, args.SelectedDrones, DroneOrderType.Target, GetCoordinates(args.TargetCoordinates), args.Actor);
    }

    private void OnPacketReceived(Entity<DroneControlComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? cmd)
            || !args.Data.TryGetValue(DroneConsoleConstants.TargetCoords, out EntityCoordinates coords)
        )
            return;

        // A drone that has been claimed by a carrier is driven by AutoDroneSystem, so route the manual order
        // there as a temporary override. An unclaimed auto-drone falls through to the HTN path below so it
        // still responds to console orders.
        if (TryComp<AutoDroneComponent>(ent, out var autoDrone) && autoDrone.CarrierConsole != null)
        {
            autoDrone.ManualCommand = cmd;
            autoDrone.ManualTarget = coords;
            autoDrone.ManualOverrideUntil = _timing.CurTime + autoDrone.ManualOverrideTimeout;
            return;
        }

        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var blackboard = htn.Blackboard;

        if (!blackboard.TryGetValue<string>(ent.Comp.OrderKey, out var nowCmd, EntityManager) || !nowCmd.Equals(cmd))
            _htn.ShutdownPlan(htn);

        blackboard.SetValue(ent.Comp.OrderKey, cmd);
        blackboard.SetValue(ent.Comp.TargetKey, coords);
    }

    private void DoTargetedDroneOrder(Entity<DroneControlConsoleComponent> console, HashSet<NetEntity> selected, DroneOrderType order, EntityCoordinates coordinates, EntityUid actor)
    {
        if (!coordinates.TryDistance(EntityManager, Transform(console).Coordinates, out var distance))
            return;

        if (distance > (console.Comp.MaxOrderRadius ?? float.MaxValue))
        {
            _popup.PopupEntity(Loc.GetString("drone-control-out-of-range"), console, PopupType.Medium);
            return;
        }

        if (!TryComp<DroneCarrierComponent>(console, out var carrier))
            return;

        var command = order == DroneOrderType.Move ? DroneConsoleConstants.CommandMove : DroneConsoleConstants.CommandTarget;

        // Set the manual override directly on the selected claimed drones. This works on ANY clicked grid,
        // including a friendly one (in case that ship has been captured by the enemy).
        foreach (var drone in carrier.Slots.Values)
        {
            if (!selected.Contains(GetNetEntity(drone)) || !TryComp<AutoDroneComponent>(drone, out var ad))
                continue;

            ad.ManualCommand = command;
            ad.ManualTarget = coordinates;
            ad.ManualOverrideUntil = _timing.CurTime + ad.ManualOverrideTimeout;
        }
    }

    private void SendToSelected(EntityUid source, HashSet<NetEntity> selected, NetworkPayload payload)
    {
        if (!TryComp<DeviceListComponent>(source, out var devList))
            return;

        var linked = _deviceList.GetDeviceList(source, devList);

        foreach (var (name, droneUid) in linked)
        {
            if (selected.Contains(GetNetEntity(droneUid)) && TryComp<DeviceNetworkComponent>(droneUid, out var droneNet))
                _deviceNetwork.QueuePacket(source, droneNet.Address, payload);
        }
    }

    private void UpdateState(EntityUid console)
    {
        var nav = _shuttleConsole.GetNavState(console, _shuttleConsole.GetAllDocks());
        var iffState = _shuttleConsole.GetIFFState(console, null);

        // The carrier's own slot roster is authoritative - it always matches the drones it commands.
        var drones = new List<(NetEntity, NetEntity)>();
        var isCarrier = TryComp<DroneCarrierComponent>(console, out var carrier);

        if (carrier != null)
        {
            foreach (var drone in carrier.Slots.Values)
            {
                if (TerminatingOrDeleted(drone))
                    continue;

                var xform = Transform(drone);
                if (xform.GridUid == null)
                    continue;

                drones.Add((GetNetEntity(drone), GetNetEntity(xform.GridUid.Value)));
            }
        }

        _ui.SetUiState(console, DroneConsoleUiKey.Key, new DroneConsoleBoundUserInterfaceState(
            nav, iffState, drones,
            isCarrier,
            carrier?.Stance ?? DroneStance.Attack,
            carrier?.Targeting ?? DroneTargeting.Enemies,
            carrier?.Formation ?? DroneFormation.Arrow,
            carrier?.ProducedCount ?? 0,
            carrier?.MaxDrones ?? 0,
            carrier?.SpawnableDrones ?? new List<string>()));
    }

    public void TryAutolink(EntityUid fromEnt)
    {
        var newDrones = new List<EntityUid>();

        var xform = Transform(fromEnt);
        var shipUid = xform.GridUid;
        if (!TryComp<MapGridComponent>(shipUid, out var grid))
            return;

        _docks.Clear();
        _lookup.GetLocalEntitiesIntersecting(shipUid.Value, grid.LocalAABB, _docks);

        foreach (var dock in _docks)
        {
            if (dock.Comp.DockedWith == null)
                continue;

            var withXform = Transform(dock.Comp.DockedWith.Value);

            if (!TryComp<MapGridComponent>(withXform.GridUid, out var withGrid))
                continue;

            _controllers.Clear();
            _lookup.GetLocalEntitiesIntersecting(withXform.GridUid.Value, withGrid.LocalAABB, _controllers);
            foreach (var controller in _controllers)
            {
                if (!_controlQuery.TryComp(controller, out var controlComp) || controlComp.Autolinked)
                    continue;

                controlComp.Autolinked = true;
                newDrones.Add(controller);
            }
        }

        if (newDrones.Count != 0)
            _deviceList.UpdateDeviceList(fromEnt, newDrones, true);

        _popup.PopupEntity(Loc.GetString("drone-control-autolinked", ("count", newDrones.Count)), fromEnt, PopupType.Large);
    }
}
