using System.Numerics;
using Content.Server._Crescent.Diplomacy;
using Content.Server._Mono.NPC.HTN;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shipyard;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Crescent.Diplomacy;
using Content.Shared._Crescent.DroneControl;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Popups;
using Content.Shared.Shipyard.Prototypes;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.DroneControl;

/// <summary>
///     Drives <see cref="AutoDroneComponent"/> drones for a <see cref="DroneCarrierComponent"/> console:
///     claims drones docked to (or linked to) the carrier, undocks them, holds them in a selectable
///     formation, and directs them to focus-fire diplomatic enemies. Manual console orders temporarily
///     override the autopilot (routed here from <see cref="DroneControlSystem"/>).
/// </summary>
public sealed class AutoDroneSystem : EntitySystem
{
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly DiplomacySystem _diplomacy = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShipSteeringSystem _steering = default!;
    [Dependency] private readonly ShipTargetingSystem _targeting = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private EntityQuery<ApcPowerReceiverComponent> _powerQuery;
    private EntityQuery<AutoDroneComponent> _autoQuery;
    private EntityQuery<DroneControlComponent> _droneServerQuery;
    private EntityQuery<IFFComponent> _iffQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ShipTargetingComponent> _targetingQuery;
    private EntityQuery<ShipSteererComponent> _steererQuery;

    private const float UpdateInterval = 0.25f;
    private const int DeployEveryNTicks = 4; // ~1s between deployment scans
    private const float FriendlyHoldFireRange = 300f; // hold fire if a friendly is this close in front
    private const float PendingSpawnTtl = 20f; // seconds a produced-but-unclaimed drone counts against the cap
    private float _accumulator;
    private int _deployTick;

    // scratch collections, reused between ticks
    private readonly HashSet<Entity<DockingComponent>> _docks = new();
    private readonly HashSet<Entity<AutoDroneComponent>> _dockedDrones = new();
    private readonly HashSet<EntityUid> _enemies = new();
    private readonly HashSet<EntityUid> _enemyDrones = new(); // enemy grids that carry a drone server
    private readonly List<KeyValuePair<int, EntityUid>> _slotScratch = new();
    private List<Entity<MapGridComponent>> _gridScratch = new();

    public override void Initialize()
    {
        base.Initialize();

        _powerQuery = GetEntityQuery<ApcPowerReceiverComponent>();
        _autoQuery = GetEntityQuery<AutoDroneComponent>();
        _droneServerQuery = GetEntityQuery<DroneControlComponent>();
        _iffQuery = GetEntityQuery<IFFComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _targetingQuery = GetEntityQuery<ShipTargetingComponent>();
        _steererQuery = GetEntityQuery<ShipSteererComponent>();

        SubscribeLocalEvent<AutoDroneComponent, ComponentShutdown>(OnDroneShutdown);
        SubscribeLocalEvent<DroneCarrierComponent, ComponentShutdown>(OnCarrierShutdown);
        SubscribeLocalEvent<DroneCarrierComponent, GetVerbsEvent<AlternativeVerb>>(OnCarrierGetVerbs);

        SubscribeLocalEvent<DroneCarrierComponent, DroneConsoleDeployMessage>(OnUiDeploy);
        SubscribeLocalEvent<DroneCarrierComponent, DroneConsoleSetStanceMessage>(OnUiSetStance);
        SubscribeLocalEvent<DroneCarrierComponent, DroneConsoleSetTargetingMessage>(OnUiSetTargeting);
        SubscribeLocalEvent<DroneCarrierComponent, DroneConsoleSetFormationMessage>(OnUiSetFormation);
        SubscribeLocalEvent<DroneCarrierComponent, DroneConsoleSpawnMessage>(OnUiSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;
        _accumulator = 0f;

        var now = _timing.CurTime;
        // Claiming walks docks/station grids, so run it less often than the steering update.
        var doDeploy = _deployTick++ % DeployEveryNTicks == 0;

        var query = EntityQueryEnumerator<DroneCarrierComponent>();
        while (query.MoveNext(out var consoleUid, out var carrier))
        {
            var carrierGrid = Transform(consoleUid).GridUid;
            if (carrierGrid == null)
                continue;

            var powered = !_powerQuery.TryComp(consoleUid, out var receiver) || _power.IsPowered(consoleUid, receiver);

            if (doDeploy && powered)
                TryDeploy((consoleUid, carrier), carrierGrid.Value);

            // Find diplomatic enemies near the carrier (leash), pick a focus, then drive each drone.
            // The stance decides whether and how far the drones look for targets.
            if (carrier.Stance == DroneStance.Follow)
            {
                _enemies.Clear();
                carrier.FocusTarget = null;
            }
            else
            {
                var faction = _iffQuery.TryComp(carrierGrid, out var iff) ? iff.Faction : "Neutral";
                var range = carrier.Stance == DroneStance.Defend ? carrier.DefendRange : carrier.EngagementRange;
                ScanEnemies(carrierGrid.Value, faction, range, carrier.Targeting);
                SelectFocus(carrier, carrierGrid.Value);
            }

            DriveDrones((consoleUid, carrier), now);
        }
    }

    #region deployment

    private void TryDeploy(Entity<DroneCarrierComponent> carrier, EntityUid carrierGrid)
    {
        if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
            return;

        // Formation is anchored to the console's own grid.
        if (!_gridQuery.TryComp(carrierGrid, out var carrierGridComp))
            return;

        // 1) Drones docked directly to the carrier ship.
        ScanGridForDockedDrones(carrier, carrierGrid, carrierGridComp, carrierGrid);
        if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
            return;

        // 2) Drones docked to any grid of the console's owning station: a shipyard docks purchased drones to
        //    the station's largest grid, which is often not the same grid the drone console sits on.
        if (_station.GetOwningStation(carrier.Owner) is { } station && TryComp<StationDataComponent>(station, out var data))
        {
            foreach (var stationGrid in data.Grids)
            {
                if (stationGrid == carrierGrid)
                    continue;

                ScanGridForDockedDrones(carrier, carrierGrid, carrierGridComp, stationGrid);
                if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
                    return;
            }
        }

        // 3) Drones already linked to this console's device list (e.g. wired up with a network configurator),
        //    wherever they currently are.
        if (HasComp<DeviceListComponent>(carrier.Owner))
        {
            foreach (var (_, device) in _deviceList.GetDeviceList(carrier.Owner))
            {
                if (!_autoQuery.TryComp(device, out var ad) || ad.CarrierConsole != null)
                    continue;

                DeployDrone(carrier, carrierGrid, carrierGridComp, (device, ad));
                if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
                    return;
            }
        }

        // 4) Undocked drones of our own faction floating near the carrier. A shipyard-bought drone is FTL'd to
        //    the station and only ends up docked if a matching port was free, so it frequently just arrives
        //    alongside the hull - the dock scans above would never see it.
        ScanNearbyDrones(carrier, carrierGrid, carrierGridComp);
    }

    /// <summary>
    ///     Claims unclaimed drones within <see cref="DroneCarrierComponent.ClaimRange"/> that already fly the
    ///     carrier's own faction, regardless of whether they are docked to anything.
    /// </summary>
    private void ScanNearbyDrones(Entity<DroneCarrierComponent> carrier, EntityUid carrierGrid, MapGridComponent carrierGridComp)
    {
        var carrierPos = _transform.GetMapCoordinates(carrierGrid);
        if (carrierPos.MapId == MapId.Nullspace)
            return;

        var carrierFaction = _iffQuery.TryComp(carrierGrid, out var carrierIff) ? carrierIff.Faction : "Neutral";

        foreach (var (drone, comp) in _lookup.GetEntitiesInRange<AutoDroneComponent>(carrierPos, carrier.Comp.ClaimRange))
        {
            if (comp.CarrierConsole != null)
                continue; // already fielded by some carrier

            var droneGrid = Transform(drone).GridUid;
            if (droneGrid == null || droneGrid == carrierGrid)
                continue;

            // Same faction only: a shipyard-bought drone inherits the buying console's faction, so this picks
            // up our own purchases without poaching a neutral third party's drone.
            var droneFaction = _iffQuery.TryComp(droneGrid.Value, out var droneIff) ? droneIff.Faction : "Neutral";
            if (droneFaction != carrierFaction)
                continue;

            DeployDrone(carrier, carrierGrid, carrierGridComp, (drone, comp));

            if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
                return;
        }
    }

    /// <summary>
    ///     Finds undeployed <see cref="AutoDroneComponent"/> drones docked to <paramref name="scanGrid"/> and
    ///     deploys them into the given carrier's formation (anchored to <paramref name="carrierGrid"/>).
    /// </summary>
    private void ScanGridForDockedDrones(Entity<DroneCarrierComponent> carrier, EntityUid carrierGrid, MapGridComponent carrierGridComp, EntityUid scanGrid)
    {
        if (!_gridQuery.TryComp(scanGrid, out var grid))
            return;

        _docks.Clear();
        _lookup.GetLocalEntitiesIntersecting(scanGrid, grid.LocalAABB, _docks);

        foreach (var dock in _docks)
        {
            if (dock.Comp.DockedWith == null)
                continue;

            var partnerGrid = Transform(dock.Comp.DockedWith.Value).GridUid;
            if (partnerGrid == null || partnerGrid == scanGrid)
                continue;

            if (!_gridQuery.TryComp(partnerGrid, out var partnerGridComp))
                continue;

            // A partner grid is a valid drone only if it carries an AutoDrone control server. Anything else
            // (the station the carrier is parked at, a player ship, ...) simply has none and is skipped.
            _dockedDrones.Clear();
            _lookup.GetLocalEntitiesIntersecting(partnerGrid.Value, partnerGridComp.LocalAABB, _dockedDrones);
            foreach (var drone in _dockedDrones)
            {
                if (drone.Comp.CarrierConsole != null)
                    continue; // already deployed elsewhere

                DeployDrone(carrier, carrierGrid, carrierGridComp, drone);

                if (carrier.Comp.ProducedCount >= EffectiveMaxDrones(carrier.Comp))
                    return;
            }
        }
    }

    private void DeployDrone(Entity<DroneCarrierComponent> carrier, EntityUid carrierGrid, MapGridComponent grid, Entity<AutoDroneComponent> drone)
    {
        var droneGrid = Transform(drone.Owner).GridUid;

        var carrierFaction = _iffQuery.TryComp(carrierGrid, out var carrierIff) ? carrierIff.Faction : "Neutral";

        // Never claim an enemy's drone (e.g. one docked to the same shared station).
        if (droneGrid != null && _iffQuery.TryComp(droneGrid.Value, out var droneIff)
            && _diplomacy.GetRelations(carrierFaction, droneIff.Faction) == Relations.War)
            return;

        var slot = GetFreeSlot(carrier.Comp);
        if (slot < 0)
            return;

        carrier.Comp.Slots[slot] = drone.Owner;
        carrier.Comp.ProducedCount++; // lifetime count, never decremented (hard production cap)
        if (carrier.Comp.PendingSpawns.Count > 0)
            carrier.Comp.PendingSpawns.RemoveAt(0); // this claim accounts for one in-flight production

        drone.Comp.CarrierConsole = carrier.Owner;
        drone.Comp.Slot = slot;
        drone.Comp.SlotCoordinates = ComputeSlot(carrierGrid, grid, carrier.Comp, slot);
        drone.Comp.Mode = AutoDroneMode.Follow;

        // Adopt the carrier's faction so diplomacy and radar treat the drone consistently.
        if (droneGrid != null)
            _shuttle.SetIFFFaction(droneGrid.Value, carrierFaction);

        // Link the drone to the carrier console so it shows on the console UI and can receive manual override
        // orders after it has undocked. Mirrors the console's manual autolink.
        if (TryComp<DroneControlComponent>(drone.Owner, out var control))
            control.Autolinked = true;
        if (HasComp<DeviceListComponent>(carrier.Owner))
            _deviceList.UpdateDeviceList(carrier.Owner, new List<EntityUid> { drone.Owner }, true);

        // Cast off from the carrier (no-op if it wasn't docked).
        if (droneGrid != null)
            _docking.UndockDocks(droneGrid.Value);
    }

    private int GetFreeSlot(DroneCarrierComponent carrier)
    {
        var max = EffectiveMaxDrones(carrier);
        for (var i = 0; i < max; i++)
        {
            if (!carrier.Slots.ContainsKey(i))
                return i;
        }

        return -1;
    }

    private static int EffectiveMaxDrones(DroneCarrierComponent carrier) => Math.Max(0, carrier.MaxDrones);

    #endregion

    #region formation

    /// <summary>
    ///     Formation slot as a carrier-grid-relative coordinate, anchored behind the carrier's hull so drones
    ///     never try to sit inside it. Grid-relative, so it rotates and moves with the carrier.
    /// </summary>
    private EntityCoordinates ComputeSlot(EntityUid carrierGrid, MapGridComponent grid, DroneCarrierComponent carrier, int slot)
    {
        var aabb = grid.LocalAABB;
        var offset = GetFormationOffset(carrier, slot);
        // X centered on the hull, Y measured backward from the rear edge (shuttles face grid-north / +Y).
        return new EntityCoordinates(carrierGrid, new Vector2(aabb.Center.X + offset.X, aabb.Bottom - offset.Y));
    }

    /// <summary>
    ///     Slot offset as (lateral X, distance behind the hull). Both are positive magnitudes except X's sign.
    /// </summary>
    private static Vector2 GetFormationOffset(DroneCarrierComponent carrier, int slot)
    {
        var s = carrier.FormationSpacing;
        var d = carrier.FormationDepth;
        var n = Math.Max(1, EffectiveMaxDrones(carrier));

        switch (carrier.Formation)
        {
            case DroneFormation.Arrow:
            {
                var row = slot / 2;
                var side = slot % 2 == 0 ? -1f : 1f;
                return new Vector2(side * (row + 1) * s, (row + 1) * d);
            }
            case DroneFormation.LineAbreast:
                return new Vector2((slot - (n - 1) / 2f) * s, d);
            case DroneFormation.Column:
                return new Vector2(0f, (slot + 1) * d);
            case DroneFormation.Echelon:
                return new Vector2((slot + 1) * s, (slot + 1) * d);
            case DroneFormation.Diamond:
            {
                var cd = 2f * d;
                return slot switch
                {
                    0 => new Vector2(0f, cd - d),
                    1 => new Vector2(0f, cd + d),
                    2 => new Vector2(-s, cd),
                    3 => new Vector2(s, cd),
                    _ => new Vector2((slot - (n - 1) / 2f) * s, cd),
                };
            }
            default:
                return new Vector2(0f, d);
        }
    }

    private void RecomputeFormation(EntityUid console, DroneCarrierComponent carrier)
    {
        var carrierGrid = Transform(console).GridUid;
        if (carrierGrid == null || !_gridQuery.TryComp(carrierGrid, out var grid))
            return;

        foreach (var (slot, drone) in carrier.Slots)
        {
            if (_autoQuery.TryComp(drone, out var comp))
                comp.SlotCoordinates = ComputeSlot(carrierGrid.Value, grid, carrier, slot);
        }
    }

    private void OnCarrierGetVerbs(Entity<DroneCarrierComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Explicit deploy so players don't depend on the automatic dock scan, and to diagnose setup.
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("drone-carrier-deploy"),
            Priority = 10,
            Act = () => ForceDeploy(ent)
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("drone-carrier-cycle-formation",
                ("formation", Loc.GetString($"drone-formation-{ent.Comp.Formation.ToString().ToLowerInvariant()}"))),
            Priority = 9,
            Act = () => CycleFormation(ent)
        });

        // One verb per stance so the player picks directly; the active stance is greyed out.
        var stanceCategory = new VerbCategory(Loc.GetString("drone-stance-category"));
        foreach (var stance in Enum.GetValues<DroneStance>())
        {
            var isCurrent = ent.Comp.Stance == stance;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString($"drone-stance-{stance.ToString().ToLowerInvariant()}"),
                Category = stanceCategory,
                Priority = 8,
                Disabled = isCurrent,
                Act = () => SetStance(ent, stance)
            });
        }
    }

    private void OnUiDeploy(Entity<DroneCarrierComponent> ent, ref DroneConsoleDeployMessage args)
    {
        ForceDeploy(ent);
    }

    private void OnUiSetStance(Entity<DroneCarrierComponent> ent, ref DroneConsoleSetStanceMessage args)
    {
        SetStance(ent, args.Stance);
    }

    private void OnUiSetTargeting(Entity<DroneCarrierComponent> ent, ref DroneConsoleSetTargetingMessage args)
    {
        ent.Comp.Targeting = args.Targeting;
        _popup.PopupEntity(Loc.GetString("drone-targeting-set",
            ("targeting", Loc.GetString($"drone-targeting-{args.Targeting.ToString().ToLowerInvariant()}"))),
            ent.Owner, PopupType.Medium);
    }

    private void OnUiSetFormation(Entity<DroneCarrierComponent> ent, ref DroneConsoleSetFormationMessage args)
    {
        ent.Comp.Formation = args.Formation;
        RecomputeFormation(ent.Owner, ent.Comp);
    }

    private void OnUiSpawn(Entity<DroneCarrierComponent> ent, ref DroneConsoleSpawnMessage args)
    {
        if (!ent.Comp.SpawnableDrones.Contains(args.VesselId))
            return;

        // Drop stale in-flight spawns so a drone that never docked doesn't block production forever.
        var now = _timing.CurTime;
        ent.Comp.PendingSpawns.RemoveAll(t => (now - t).TotalSeconds > PendingSpawnTtl);

        // Hard lifetime cap: produced + still-arriving must stay under the limit.
        if (ent.Comp.ProducedCount + ent.Comp.PendingSpawns.Count >= EffectiveMaxDrones(ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("drone-carrier-limit-reached"), ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (_station.GetOwningStation(ent.Owner) is not { } station)
        {
            _popup.PopupEntity(Loc.GetString("drone-carrier-deploy-nogrid"), ent.Owner, PopupType.Medium);
            return;
        }

        if (!_proto.TryIndex<VesselPrototype>(args.VesselId, out var vessel))
            return;

        // Free production: the shipyard spawns the drone and docks it to the station; the deploy scan then
        // claims it into a formation slot (which is where ProducedCount is incremented).
        if (!_shipyard.TryPurchaseShuttle(station, vessel.Path.ToString(), out var shuttle, out _))
        {
            _popup.PopupEntity(Loc.GetString("drone-carrier-spawn-failed"), ent.Owner, PopupType.MediumCaution);
            return;
        }

        // TryPurchaseShuttle leaves the grid as an anonymous "grid"; give it the carrier's faction and a proper
        // name so it shows up on radar/the map instead of being a nameless grid.
        var carrierFaction = _iffQuery.TryComp(Transform(ent.Owner).GridUid ?? ent.Owner, out var carrierIff) ? carrierIff.Faction : "Neutral";
        _shuttle.SetIFFFaction(shuttle.Owner, carrierFaction);
        _metaData.SetEntityName(shuttle.Owner, $"{vessel.Name} {_random.Next(100, 1000)}");

        ent.Comp.PendingSpawns.Add(now);
        _popup.PopupEntity(Loc.GetString("drone-carrier-spawned"), ent.Owner, PopupType.Medium);
    }

    private void ForceDeploy(Entity<DroneCarrierComponent> ent)
    {
        var carrierGrid = Transform(ent.Owner).GridUid;
        if (carrierGrid == null)
        {
            _popup.PopupEntity(Loc.GetString("drone-carrier-deploy-nogrid"), ent.Owner, PopupType.Medium);
            return;
        }

        var before = ent.Comp.Slots.Count;
        TryDeploy(ent, carrierGrid.Value);
        var deployed = ent.Comp.Slots.Count - before;

        _popup.PopupEntity(Loc.GetString("drone-carrier-deployed", ("count", deployed)), ent.Owner, PopupType.Medium);
    }

    private void SetStance(Entity<DroneCarrierComponent> ent, DroneStance stance)
    {
        ent.Comp.Stance = stance;

        if (stance == DroneStance.Follow)
            ent.Comp.FocusTarget = null;

        _popup.PopupEntity(Loc.GetString("drone-stance-set",
            ("stance", Loc.GetString($"drone-stance-{stance.ToString().ToLowerInvariant()}"))),
            ent.Owner, PopupType.Medium);
    }

    private void CycleFormation(Entity<DroneCarrierComponent> ent)
    {
        var values = Enum.GetValues<DroneFormation>();
        var next = (Array.IndexOf(values, ent.Comp.Formation) + 1) % values.Length;
        ent.Comp.Formation = values[next];

        RecomputeFormation(ent.Owner, ent.Comp);

        _popup.PopupEntity(Loc.GetString("drone-carrier-formation-set",
            ("formation", Loc.GetString($"drone-formation-{ent.Comp.Formation.ToString().ToLowerInvariant()}"))),
            ent.Owner, PopupType.Medium);
    }

    #endregion

    #region behavior

    private void DriveDrones(Entity<DroneCarrierComponent> carrier, TimeSpan now)
    {
        var focus = carrier.Comp.FocusTarget;
        var hasFocus = focus != null && !TerminatingOrDeleted(focus.Value);

        // Copy since undeploy mutates the dictionary.
        _slotScratch.Clear();
        _slotScratch.AddRange(carrier.Comp.Slots);

        foreach (var (slot, droneUid) in _slotScratch)
        {
            if (TerminatingOrDeleted(droneUid) || !_autoQuery.TryComp(droneUid, out var drone) || Transform(droneUid).GridUid == null)
            {
                UndeploySlot(carrier, slot);
                continue;
            }

            // Unpowered drones drift.
            if (_powerQuery.TryComp(droneUid, out var receiver) && !_power.IsPowered(droneUid, receiver))
            {
                StopDrone(droneUid);
                drone.Mode = AutoDroneMode.Idle;
                continue;
            }

            // A recent manual console order takes priority.
            if (now < drone.ManualOverrideUntil && drone.ManualCommand != null)
            {
                DriveManual(droneUid, drone, carrier.Comp);
                continue;
            }

            if (hasFocus)
                DriveAttack(droneUid, drone, focus!.Value, carrier.Comp);
            else
                DriveFollow(droneUid, drone, carrier.Comp);
        }
    }

    private void DriveFollow(EntityUid drone, AutoDroneComponent comp, DroneCarrierComponent carrier)
    {
        var carrierGrid = comp.SlotCoordinates.EntityId;

        // If we're still hugging the carrier (e.g. just undocked), first fly straight out to open space so we
        // don't grind against or shove the hull. The target is empty space, so the carrier IS avoided.
        if (!TerminatingOrDeleted(carrierGrid) && _gridQuery.TryComp(carrierGrid, out var carrierGridComp))
        {
            var carrierMap = _transform.GetMapCoordinates(carrierGrid);
            var droneMap = _transform.GetMapCoordinates(drone);

            if (carrierMap.MapId != MapId.Nullspace && carrierMap.MapId == droneMap.MapId)
            {
                var toDrone = droneMap.Position - carrierMap.Position;
                var dist = toDrone.Length();
                var clearRadius = carrierGridComp.LocalAABB.Size.Length() * 0.5f + carrier.LaunchClearance;

                if (dist < clearRadius && Transform(carrierGrid).MapUid is { } mapUid)
                {
                    var awayDir = dist > 0.1f ? toDrone / dist : new Vector2(0f, -1f);
                    var awayPos = carrierMap.Position + awayDir * (clearRadius + carrier.LaunchClearance);

                    var launch = _steering.Steer(drone, new EntityCoordinates(mapUid, awayPos));
                    if (launch != null)
                    {
                        launch.Mode = ShipSteeringMode.GoToRange;
                        launch.Range = 5f;
                        launch.RangeTolerance = null;
                        launch.InRangeMaxSpeed = null; // clear the hull quickly
                        launch.MaxRotateRate = null;
                        launch.LeadingEnabled = false;
                        launch.AlwaysFaceTarget = false;
                        launch.AvoidCollisions = true;
                        launch.AvoidTargetGrid = false; // target is empty space
                    }

                    StopFiring(drone);
                    comp.Mode = AutoDroneMode.Launching;
                    return;
                }
            }
        }

        // Clear of the hull: hold the formation slot, matching the carrier's velocity and routing around it.
        var steer = _steering.Steer(drone, comp.SlotCoordinates);
        if (steer == null)
            return;

        steer.Mode = ShipSteeringMode.GoToRange;
        steer.Range = carrier.FormationRange;
        steer.RangeTolerance = null;
        steer.InRangeMaxSpeed = 0.1f; // relative to the carrier, so drones lock velocity with it
        steer.MaxRotateRate = 0.02f; // must settle rotation before counting as arrived, so it stops spinning
        steer.LeadingEnabled = true; // match the carrier's velocity so we hold station in sync
        steer.AlwaysFaceTarget = false;
        steer.AvoidCollisions = true;
        steer.AvoidTargetGrid = true; // route around the carrier instead of ramming through it

        StopFiring(drone);
        comp.Mode = AutoDroneMode.Follow;
    }

    private void DriveAttack(EntityUid drone, AutoDroneComponent comp, EntityUid enemyGrid, DroneCarrierComponent carrier)
    {
        var enemyCoords = new EntityCoordinates(enemyGrid, Vector2.Zero);

        var steer = _steering.Steer(drone, enemyCoords);
        if (steer != null)
        {
            // Hold at a standoff distance and keep the nose on the target instead of orbiting/spinning; each
            // drone sits at a slightly different range so they spread into a firing line rather than stacking.
            steer.Mode = ShipSteeringMode.GoToRange;
            steer.Range = carrier.OrbitRange + comp.Slot * 25f;
            steer.RangeTolerance = 40f;
            steer.InRangeMaxSpeed = 0.1f; // settle and hold, don't circle
            steer.MaxRotateRate = 0.02f; // must settle rotation before counting as arrived, so it stops spinning
            steer.LeadingEnabled = true;
            steer.AlwaysFaceTarget = true;
            steer.AvoidCollisions = true;
            steer.AvoidTargetGrid = false;
        }

        comp.Mode = AutoDroneMode.Attack;

        // Hold fire if a friendly ship is in the line of fire toward the target, so we never clip an ally.
        var faction = _iffQuery.TryComp(Transform(drone).GridUid ?? drone, out var iff) ? iff.Faction : "Neutral";
        if (FriendlyInLineOfFire(drone, enemyGrid, faction))
            StopFiring(drone);
        else
            _targeting.Target(drone, enemyCoords);
    }

    /// <summary>
    ///     True if a friendly (non-War) grid sits within <see cref="FriendlyHoldFireRange"/> of the drone and
    ///     roughly between it and the target - i.e. in the line of fire.
    /// </summary>
    private bool FriendlyInLineOfFire(EntityUid drone, EntityUid enemyGrid, string faction)
    {
        var droneGrid = Transform(drone).GridUid;
        if (droneGrid == null)
            return false;

        var dronePos = _transform.GetMapCoordinates(droneGrid.Value);
        if (dronePos.MapId == MapId.Nullspace)
            return false;

        var toEnemy = _transform.GetMapCoordinates(enemyGrid).Position - dronePos.Position;
        if (toEnemy.LengthSquared() < 0.01f)
            return false;
        toEnemy = toEnemy.Normalized();

        var bounds = Box2.CenteredAround(dronePos.Position, new Vector2(FriendlyHoldFireRange * 2f, FriendlyHoldFireRange * 2f));
        _gridScratch.Clear();
        _mapManager.FindGridsIntersecting(dronePos.MapId, bounds, ref _gridScratch, approx: true, includeMap: false);

        foreach (var grid in _gridScratch)
        {
            if (grid.Owner == droneGrid.Value || grid.Owner == enemyGrid)
                continue;

            var toGrid = _transform.GetMapCoordinates(grid.Owner).Position - dronePos.Position;
            var distSq = toGrid.LengthSquared();
            if (distSq > FriendlyHoldFireRange * FriendlyHoldFireRange || distSq < 0.01f)
                continue;

            // In front, within ~30 degrees of the firing line?
            if (Vector2.Dot(toGrid.Normalized(), toEnemy) < 0.86f)
                continue;

            var gridFaction = _iffQuery.TryComp(grid.Owner, out var iff) ? iff.Faction : "Neutral";
            if (_diplomacy.GetRelations(faction, gridFaction) != Relations.War)
                return true; // a friendly/neutral grid is in the line of fire
        }

        return false;
    }

    private void DriveManual(EntityUid drone, AutoDroneComponent comp, DroneCarrierComponent carrier)
    {
        var target = comp.ManualTarget;

        if (comp.ManualCommand == DroneConsoleConstants.CommandTarget)
        {
            var steer = _steering.Steer(drone, target);
            if (steer != null)
            {
                // Hold at standoff facing the target, don't orbit/spin.
                steer.Mode = ShipSteeringMode.GoToRange;
                steer.Range = carrier.OrbitRange;
                steer.RangeTolerance = 40f;
                steer.InRangeMaxSpeed = 0.1f;
                steer.MaxRotateRate = 0.02f;
                steer.LeadingEnabled = true;
                steer.AlwaysFaceTarget = true;
                steer.AvoidCollisions = true;
                steer.AvoidTargetGrid = false;
            }

            _targeting.Target(drone, target);
        }
        else // move
        {
            var steer = _steering.Steer(drone, target);
            if (steer != null)
            {
                steer.Mode = ShipSteeringMode.GoToRange;
                steer.Range = 15f;
                steer.RangeTolerance = null;
                steer.InRangeMaxSpeed = 0.1f;
                steer.MaxRotateRate = 0.02f;
                steer.LeadingEnabled = false;
                steer.AlwaysFaceTarget = true;
                steer.AvoidCollisions = true;
                steer.AvoidTargetGrid = false;
            }

            StopFiring(drone);
        }

        comp.Mode = AutoDroneMode.Manual;
    }

    #endregion

    #region targeting

    private void ScanEnemies(EntityUid carrierGrid, string faction, float range, DroneTargeting targeting)
    {
        _enemies.Clear();
        _enemyDrones.Clear();

        var carrierPos = _transform.GetMapCoordinates(carrierGrid);
        if (carrierPos.MapId == MapId.Nullspace)
            return;

        foreach (var (target, targetComp) in _lookup.GetEntitiesInRange<ShipNpcTargetComponent>(carrierPos, range))
        {
            var targetGrid = Transform(target).GridUid;
            if (targetComp.NeedGrid && targetGrid == null)
                continue;
            if (targetGrid == null || targetGrid == carrierGrid)
                continue;
            // A ship that has lost power counts as neutralized - stop wasting fire on it.
            if (targetComp.NeedPower && _powerQuery.TryComp(target, out var receiver) && !_power.IsPowered(target, receiver))
                continue;

            var targetFaction = _iffQuery.TryComp(targetGrid, out var iff) ? iff.Faction : "Neutral";
            var relation = _diplomacy.GetRelations(faction, targetFaction);
            // Enemies mode: only War. All mode: everything except friendly (allied/same-faction).
            var hostile = targeting == DroneTargeting.All ? relation != Relations.Ally : relation == Relations.War;
            if (!hostile)
                continue;

            _enemies.Add(targetGrid.Value);

            // An enemy whose targetable point is a drone control server is an enemy drone; we prioritise
            // killing those first (destroying the server disables the drone).
            if (_droneServerQuery.HasComp(target))
                _enemyDrones.Add(targetGrid.Value);
        }
    }

    /// <summary>
    ///     Picks the shared focus target with focus-fire + priority: enemy drones are hunted first (kill their
    ///     server), and only once none remain do the drones turn on other ships. The current target is kept
    ///     while it is still valid at the current priority tier.
    /// </summary>
    private void SelectFocus(DroneCarrierComponent carrier, EntityUid carrierGrid)
    {
        // Enemy drones present: focus one of them (destroy the servers first).
        if (_enemyDrones.Count > 0)
        {
            if (carrier.FocusTarget is { } droneFocus && !TerminatingOrDeleted(droneFocus) && _enemyDrones.Contains(droneFocus))
                return;

            carrier.FocusTarget = Nearest(_enemyDrones, carrierGrid);
            return;
        }

        // No enemy drones left: focus any enemy ship.
        if (_enemies.Count == 0)
        {
            carrier.FocusTarget = null;
            return;
        }

        if (carrier.FocusTarget is { } current && !TerminatingOrDeleted(current) && _enemies.Contains(current))
            return;

        carrier.FocusTarget = Nearest(_enemies, carrierGrid);
    }

    private EntityUid? Nearest(HashSet<EntityUid> grids, EntityUid fromGrid)
    {
        var fromPos = _transform.GetMapCoordinates(fromGrid).Position;

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var grid in grids)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            var dist = (_transform.GetMapCoordinates(grid).Position - fromPos).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = grid;
            }
        }

        return best;
    }

    #endregion

    #region cleanup

    private void OnDroneShutdown(Entity<AutoDroneComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.CarrierConsole is not { } console || !TryComp<DroneCarrierComponent>(console, out var carrier))
            return;

        if (ent.Comp.Slot >= 0 && carrier.Slots.TryGetValue(ent.Comp.Slot, out var occupant) && occupant == ent.Owner)
            carrier.Slots.Remove(ent.Comp.Slot);
    }

    private void OnCarrierShutdown(Entity<DroneCarrierComponent> ent, ref ComponentShutdown args)
    {
        foreach (var drone in ent.Comp.Slots.Values)
        {
            if (!_autoQuery.TryComp(drone, out var comp))
                continue;

            comp.CarrierConsole = null;
            comp.Slot = -1;
            comp.Mode = AutoDroneMode.Idle;

            if (!TerminatingOrDeleted(drone))
                StopDrone(drone);
        }

        ent.Comp.Slots.Clear();
    }

    private void UndeploySlot(Entity<DroneCarrierComponent> carrier, int slot)
    {
        if (!carrier.Comp.Slots.Remove(slot, out var drone))
            return;

        if (!_autoQuery.TryComp(drone, out var comp))
            return;

        comp.CarrierConsole = null;
        comp.Slot = -1;
        comp.Mode = AutoDroneMode.Idle;

        if (!TerminatingOrDeleted(drone))
            StopDrone(drone);
    }

    private void StopDrone(EntityUid drone)
    {
        if (_steererQuery.HasComp(drone))
            _steering.Stop(drone);
        StopFiring(drone);
    }

    private void StopFiring(EntityUid drone)
    {
        if (_targetingQuery.HasComp(drone))
            _targeting.Stop(drone);
    }

    #endregion
}
