// Ratgore Start
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server._Crescent.SpaceArtillery;
using Content.Server.Power.Components;
using Content.Shared._Crescent.HardlinerFireControl;
using Content.Shared.CombatMode;
using Content.Shared.DeviceNetwork;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.HardlinerFireControl;

/// <summary>
/// Answers capacitor / magazine telemetry queries from <see cref="HardlinerFireControlConsoleSystem"/>.
/// </summary>
public sealed class HardlinerArmamentTelemetrySystem : EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HardlinerArmamentTelemetryComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    private void OnPacketReceived(EntityUid uid, HardlinerArmamentTelemetryComponent _, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? cmd) ||
            cmd != HardlinerTelemetryCommands.CmdRequest)
        {
            return;
        }

        if (!HasComp<HardlinerFireControlConsoleComponent>(args.Sender))
            return;

        if (!TryComp<DeviceNetworkComponent>(uid, out var weaponNet) ||
            weaponNet.TransmitFrequency == null)
            return;

        var payload = BuildPayload(uid);
        payload[DeviceNetworkConstants.Command] = HardlinerTelemetryCommands.CmdResponse;

        _deviceNetwork.QueuePacket(uid, args.SenderAddress, payload, device: weaponNet);
    }

    private NetworkPayload BuildPayload(EntityUid uid)
    {
        var shotCost = 4000;
        if (TryComp<SpaceArtilleryComponent>(uid, out var artillery))
            shotCost = artillery.PowerUseActive;

        var capacitorJoules = 0;
        if (TryComp<BatteryComponent>(uid, out var gunBattery))
            capacitorJoules = (int) gunBattery.CurrentCharge;

        var capFrac = shotCost <= 0
            ? 1f
            : Math.Clamp(capacitorJoules / (float) shotCost, 0f, 1f);

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(uid, ref ammoEv, broadcast: false);

        var gridPowered = true;
        if (TryComp<ApcPowerReceiverComponent>(uid, out var apc))
            gridPowered = apc.Powered;

        var armed = true;
        if (TryComp<SpaceArtilleryComponent>(uid, out var sa))
            armed = sa.IsArmed;
        else if (TryComp<CombatModeComponent>(uid, out var combat))
            armed = combat.IsInCombatMode;

        var recycleFrac = 1f;
        if (TryComp<GunComponent>(uid, out var gun))
        {
            var now = _timing.CurTime;
            if (gun.NextFire > now && gun.BurstCooldown > 0)
            {
                var remain = (gun.NextFire - now).TotalSeconds;
                recycleFrac = (float) Math.Clamp(1d - remain / gun.BurstCooldown, 0d, 1d);
            }
        }

        return new NetworkPayload
        {
            [HardlinerTelemetryCommands.KeyCapacitorFraction] = capFrac,
            [HardlinerTelemetryCommands.KeyCapacitorJoules] = capacitorJoules,
            [HardlinerTelemetryCommands.KeyShotEnergyJoules] = shotCost,
            [HardlinerTelemetryCommands.KeyShotsRemaining] = ammoEv.Count,
            [HardlinerTelemetryCommands.KeyShotsCapacity] = ammoEv.Capacity,
            [HardlinerTelemetryCommands.KeyGridPowered] = gridPowered,
            [HardlinerTelemetryCommands.KeyArmed] = armed,
            [HardlinerTelemetryCommands.KeyRecycleFraction] = recycleFrac,
        };
    }
}
// Ratgore End
