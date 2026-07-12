// Ratgore Start
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared._Crescent.HardlinerFireControl;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Crescent.HardlinerFireControl;

public sealed class HardlinerFireControlConsoleSystem : EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, ConsoleRuntimeState> _runtime = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, DeviceListUpdateEvent>(OnDeviceListUpdated);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, NewLinkEvent>(OnTelemetryLinked);
        SubscribeLocalEvent<HardlinerFireControlConsoleComponent, PortDisconnectedEvent>(OnTelemetryUnlinked);

        UpdatesBefore.Add(typeof(UserInterfaceSystem));
    }

    private void OnConsoleStartup(EntityUid uid, HardlinerFireControlConsoleComponent component, ComponentStartup args)
    {
        _runtime[uid] = new ConsoleRuntimeState();
    }

    private void OnConsoleShutdown(EntityUid uid, HardlinerFireControlConsoleComponent _, ComponentShutdown args)
    {
        _runtime.Remove(uid);
    }

    private void OnUiOpened(EntityUid uid, HardlinerFireControlConsoleComponent _, BoundUIOpenedEvent args)
    {
        RequestFreshTelemetry(uid);
        PushUi(uid);
    }

    private void OnTelemetryLinked(EntityUid uid, HardlinerFireControlConsoleComponent _, NewLinkEvent args)
    {
        if (args.Sink != uid)
            return;

        if (args.SourcePort != HardlinerTelemetryPorts.Source || args.SinkPort != HardlinerTelemetryPorts.Sink)
            return;

        if (!HasComp<HardlinerArmamentTelemetryComponent>(args.Source))
            return;

        var result = _deviceList.UpdateDeviceList(uid, new[] { args.Source }, merge: true);
        if (result != DeviceListUpdateResult.UpdateOk)
            return;

        RequestFreshTelemetry(uid);
        PushUi(uid);
    }

    private void OnTelemetryUnlinked(EntityUid uid, HardlinerFireControlConsoleComponent _, PortDisconnectedEvent args)
    {
        if (args.Port != HardlinerTelemetryPorts.Sink)
            return;

        var weapon = args.RemovedPortUid;
        var remaining = _deviceList.GetAllDevices(uid).Where(e => e != weapon).ToList();
        _deviceList.UpdateDeviceList(uid, remaining, merge: false);

        RequestFreshTelemetry(uid);
        PushUi(uid);
    }

    private void OnDeviceListUpdated(EntityUid uid, HardlinerFireControlConsoleComponent _, DeviceListUpdateEvent args)
    {
        if (!_runtime.TryGetValue(uid, out var state))
            return;

        if (!TryComp<DeviceListComponent>(uid, out var list))
            return;

        PruneRows(state, _deviceList.GetAllDevices(uid, list));
        RequestFreshTelemetry(uid);
        PushUi(uid);
    }

    private void OnPacketReceived(EntityUid uid, HardlinerFireControlConsoleComponent _, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? cmd) ||
            cmd != HardlinerTelemetryCommands.CmdResponse)
            return;

        if (!TryComp<DeviceListComponent>(uid, out var list) ||
            !_deviceList.GetAllDevices(uid, list).Contains(args.Sender))
            return;

        if (!_runtime.TryGetValue(uid, out var state))
            return;

        if (!TryComp<DeviceNetworkComponent>(args.Sender, out var senderNet))
            return;

        var row = RowFromPayload(senderNet.Address, args.Data);
        state.Rows[senderNet.Address] = row;
        PushUi(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var consoles = EntityQueryEnumerator<HardlinerFireControlConsoleComponent, DeviceListComponent, DeviceNetworkComponent, UserInterfaceComponent>();
        while (consoles.MoveNext(out var uid, out var consoleComp, out var list, out var consoleNet, out var uiComp))
        {
            if (!_runtime.TryGetValue(uid, out var state))
                continue;

            if (!_ui.IsUiOpen((uid, uiComp), HardlinerFireControlConsoleUiKey.Key))
                continue;

            var now = _timing.CurTime;
            if (now < state.NextPoll)
                continue;

            state.NextPoll = now + consoleComp.PollInterval;

            var devices = _deviceList.GetAllDevices(uid, list).ToArray();

            PruneRows(state, devices);

            if (devices.Length == 0)
            {
                PushUi(uid);
                continue;
            }

            var payload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = HardlinerTelemetryCommands.CmdRequest,
            };

            foreach (var deviceUid in devices)
            {
                if (!TryComp<DeviceNetworkComponent>(deviceUid, out var weaponNet) ||
                    weaponNet.Address == string.Empty)
                    continue;

                _deviceNetwork.QueuePacket(uid, weaponNet.Address, payload, device: consoleNet);
            }
        }
    }

    private void RequestFreshTelemetry(EntityUid uid)
    {
        if (!TryComp<DeviceListComponent>(uid, out var list) ||
            !TryComp<DeviceNetworkComponent>(uid, out var consoleNet))
            return;

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = HardlinerTelemetryCommands.CmdRequest,
        };

        foreach (var deviceUid in _deviceList.GetAllDevices(uid, list))
        {
            if (!TryComp<DeviceNetworkComponent>(deviceUid, out var weaponNet) ||
                weaponNet.Address == string.Empty)
                continue;

            _deviceNetwork.QueuePacket(uid, weaponNet.Address, payload, device: consoleNet);
        }
    }

    private void PruneRows(ConsoleRuntimeState state, IEnumerable<EntityUid> devices)
    {
        var validAddresses = new HashSet<string>();
        foreach (var ent in devices)
        {
            if (TryComp<DeviceNetworkComponent>(ent, out var net) && net.Address != string.Empty)
                validAddresses.Add(net.Address);
        }

        foreach (var key in state.Rows.Keys.ToArray())
        {
            if (!validAddresses.Contains(key))
                state.Rows.Remove(key);
        }
    }

    private HardlinerTelemetryRowState RowFromPayload(string address, NetworkPayload data)
    {
        data.TryGetValue(HardlinerTelemetryCommands.KeyCapacitorFraction, out float capFrac);
        data.TryGetValue(HardlinerTelemetryCommands.KeyCapacitorJoules, out int capJ);
        data.TryGetValue(HardlinerTelemetryCommands.KeyShotEnergyJoules, out int shotJ);
        data.TryGetValue(HardlinerTelemetryCommands.KeyShotsRemaining, out int shots);
        data.TryGetValue(HardlinerTelemetryCommands.KeyShotsCapacity, out int shotsMax);
        data.TryGetValue(HardlinerTelemetryCommands.KeyGridPowered, out bool powered);
        data.TryGetValue(HardlinerTelemetryCommands.KeyArmed, out bool armed);
        data.TryGetValue(HardlinerTelemetryCommands.KeyRecycleFraction, out float recycle);

        return new HardlinerTelemetryRowState
        {
            DeviceAddress = address,
            CapacitorFraction = capFrac,
            CapacitorJoules = capJ,
            ShotEnergyJoules = shotJ,
            ShotsRemaining = shots,
            ShotsCapacity = shotsMax,
            GridPowered = powered,
            Armed = armed,
            RecycleFraction = recycle,
        };
    }

    private void PushUi(EntityUid uid)
    {
        if (!_runtime.TryGetValue(uid, out var state))
            return;

        var ordered = state.Rows.Values
            .OrderBy(r => r.DeviceAddress, StringComparer.Ordinal)
            .ToList();

        var buiState = new HardlinerFireControlConsoleBoundUserInterfaceState(ordered);
        _ui.SetUiState(uid, HardlinerFireControlConsoleUiKey.Key, buiState);

        var msg = new HardlinerFireControlConsoleBoundUserInterfaceMessage(ordered);
        _ui.ServerSendUiMessage(uid, HardlinerFireControlConsoleUiKey.Key, msg);
    }

    private sealed class ConsoleRuntimeState
    {
        public Dictionary<string, HardlinerTelemetryRowState> Rows = new();

        public TimeSpan NextPoll;
    }
}
// Ratgore End
