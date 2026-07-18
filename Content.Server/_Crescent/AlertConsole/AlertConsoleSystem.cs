using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Crescent.AlertConsole;
using Content.Shared.Customization.Systems;
using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.AlertConsole;

public sealed class AlertConsoleSystem : EntitySystem
{
    private const int MaxMessageLength = 300;

    private static readonly Dictionary<string, string> FactionRadioChannels = new()
    {
        ["DSM"] = "Imperial",
        ["NCWL"] = "NCWL",
        ["SHI"] = "SHI",
        ["SRM"] = "Hunter",
        ["TAP"] = "Families",
        ["TFSC"] = "Syndicate",
        ["IPM"] = "Interdyne",
        ["SAW"] = "Saws",
        ["GSC"] = "Gorlex",
        ["CD"] = "Cyberdawn",
        ["TSP"] = "Nfsd",
        ["ATH"] = "Authority",
    };

    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AlertConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AlertConsoleComponent, AlertConsoleSaveSettingsMessage>(OnSaveSettings);
    }

    private void OnInit(Entity<AlertConsoleComponent> ent, ref ComponentInit args)
    {
        TryResolveFactionChannel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var consoleQuery = EntityQueryEnumerator<AlertConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Enabled)
                continue;

            comp.ScanAccumulator += frameTime;
            if (comp.ScanAccumulator < comp.ScanInterval)
                continue;
            comp.ScanAccumulator = 0f;

            if (xform.MapID == MapId.Nullspace)
                continue;

            var consolePos = xform.WorldPosition;
            var consoleMap = xform.MapID;
            var now = _timing.CurTime;

            var stale = new List<EntityUid>();
            foreach (var (eid, last) in comp.AlertCooldowns)
            {
                if (now - last > TimeSpan.FromSeconds(comp.AlertCooldownSeconds * 3))
                    stale.Add(eid);
            }
            foreach (var s in stale)
                comp.AlertCooldowns.Remove(s);

            var hasFactionChannel = !string.IsNullOrEmpty(comp.FactionChannel) &&
                                    _prototype.TryIndex<RadioChannelPrototype>(comp.FactionChannel, out _);
            var hasCommonChannel = _prototype.TryIndex<RadioChannelPrototype>("Common", out _);

            var gridQuery = EntityQueryEnumerator<IFFComponent, ShuttleComponent, PhysicsComponent, TransformComponent>();
            while (gridQuery.MoveNext(out var gridUid, out var iff, out _, out var physics, out var gridXform))
            {
                if (gridXform.MapID != consoleMap)
                    continue;

                if (xform.GridUid != null && gridUid == xform.GridUid)
                    continue;

                if ((iff.Flags & IFFFlags.Hide) != 0)
                    continue;

                if (physics.LinearVelocity.Length() < comp.MinDetectionVelocity)
                    continue;

                var dist = (gridXform.WorldPosition - consolePos).Length();
                if (dist > comp.DetectionRadius)
                    continue;

                if (comp.AlertCooldowns.TryGetValue(gridUid, out var lastAlert) &&
                    now - lastAlert < TimeSpan.FromSeconds(comp.AlertCooldownSeconds))
                    continue;

                comp.AlertCooldowns[gridUid] = now;

                var shuttleName = _shuttle.GetIFFLabel(gridUid) ?? MetaData(gridUid).EntityName;
                var distStr = ((int) dist).ToString();

                if (hasFactionChannel && !string.IsNullOrWhiteSpace(comp.StationAlertMessage))
                {
                    var msg = comp.StationAlertMessage
                        .Replace("{name}", shuttleName)
                        .Replace("{dist}", distStr);
                    _radio.SendRadioMessage(uid, msg, comp.FactionChannel, uid);
                }

                if (comp.BroadcastToShuttle && hasCommonChannel &&
                    !string.IsNullOrWhiteSpace(comp.ShuttleAlertMessage))
                {
                    var msg = comp.ShuttleAlertMessage
                        .Replace("{name}", shuttleName)
                        .Replace("{dist}", distStr);
                    _radio.SendRadioMessage(uid, msg, "Common", uid);
                }
            }
        }
    }

    private void OnUiOpened(EntityUid uid, AlertConsoleComponent comp, BoundUIOpenedEvent args)
    {
        // On a mainframe this entity hosts several UIs that all open together, so this fires once per
        // key. Without the key check we'd re-push the alert state several times as the console opens,
        // each push overwriting whatever the operator is mid-way through typing on the alert tab.
        if (!Equals(args.UiKey, AlertConsoleUiKey.Key))
            return;

        TryResolveFactionChannel((uid, comp));
        UpdateUiState(uid, comp);
    }

    private void OnSaveSettings(EntityUid uid, AlertConsoleComponent comp, AlertConsoleSaveSettingsMessage args)
    {
        comp.Enabled = args.Enabled;
        comp.DetectionRadius = Math.Clamp(args.DetectionRadius, 10f, 2000f);
        comp.StationAlertMessage = args.StationAlertMessage.Length > MaxMessageLength
            ? args.StationAlertMessage[..MaxMessageLength]
            : args.StationAlertMessage;
        comp.BroadcastToShuttle = args.BroadcastToShuttle;
        comp.ShuttleAlertMessage = args.ShuttleAlertMessage.Length > MaxMessageLength
            ? args.ShuttleAlertMessage[..MaxMessageLength]
            : args.ShuttleAlertMessage;
        comp.AlertCooldownSeconds = Math.Clamp(args.AlertCooldownSeconds, 5f, 3600f);
        Dirty(uid, comp);
        UpdateUiState(uid, comp);
    }

    private void TryResolveFactionChannel(Entity<AlertConsoleComponent> ent)
    {
        var station = _station.GetOwningStation(ent.Owner);
        if (station == null)
            return;

        var factionId = DetectStationFaction(station.Value);
        if (factionId == null)
            return;

        if (!FactionRadioChannels.TryGetValue(factionId, out var channel))
            return;

        if (!_prototype.HasIndex<RadioChannelPrototype>(channel))
            return;

        if (ent.Comp.FactionChannel == channel)
            return;

        ent.Comp.FactionChannel = channel;
        Dirty(ent);
    }

    private string? DetectStationFaction(EntityUid station)
    {
        if (!TryComp<StationJobsComponent>(station, out var jobs))
            return null;

        var counts = new Dictionary<string, int>();
        foreach (var jobId in jobs.JobList.Keys)
        {
            if (!_prototype.TryIndex<JobPrototype>(jobId, out var job))
                continue;

            var faction = GetJobFaction(job);
            if (faction == null)
                continue;

            counts.TryGetValue(faction, out var count);
            counts[faction] = count + 1;
        }

        if (counts.Count == 0)
            return null;

        return counts.MaxBy(kv => kv.Value).Key;
    }

    private static string? GetJobFaction(JobPrototype job)
    {
        foreach (var req in job.Requirements ?? [])
        {
            if (req is FactionRequirement factionReq)
                return factionReq.FactionID;
        }

        return null;
    }

    private void UpdateUiState(EntityUid uid, AlertConsoleComponent comp)
    {
        var channelResolved = !string.IsNullOrEmpty(comp.FactionChannel) &&
                              _prototype.HasIndex<RadioChannelPrototype>(comp.FactionChannel);

        var state = new AlertConsoleBuiState(
            comp.Enabled,
            comp.DetectionRadius,
            channelResolved ? comp.FactionChannel : Loc.GetString("alert-console-channel-unknown"),
            channelResolved,
            comp.StationAlertMessage,
            comp.BroadcastToShuttle,
            comp.ShuttleAlertMessage,
            comp.AlertCooldownSeconds);
        _uiSystem.SetUiState(uid, AlertConsoleUiKey.Key, state);
    }
}
