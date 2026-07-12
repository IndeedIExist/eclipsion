using System.Linq;
using Content.Server.Announcements.Systems;
using Content.Shared._Crescent.HullrotFaction;
using Content.Shared._Crescent.Diplomacy;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Diplomacy;

public sealed class RatDiplomacySystem : EntitySystem
{
    [Dependency] private readonly AnnouncerSystem _announcer = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private static readonly string[] AllFactions =
        ["DSM", "NCWL", "SHI", "SRM", "TAP", "IPM", "SAW", "GSC", "CD", "TSP"];

    private readonly Dictionary<string, Dictionary<string, FactionRelation>> _relations = new();
    private readonly Dictionary<string, List<PendingProposal>> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiplomacyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyDeclareWarMessage>(OnDeclareWar);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyProposePeaceMessage>(OnProposePeace);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyProposeAllianceMessage>(OnProposeAlliance);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyProposeTradeMessage>(OnProposeTrade);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyBreakTradeMessage>(OnBreakTrade);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyAcceptProposalMessage>(OnAcceptProposal);
        SubscribeLocalEvent<DiplomacyConsoleComponent, DiplomacyRejectProposalMessage>(OnRejectProposal);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

        InitRelations();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void InitRelations()
    {
        foreach (var factionId in AllFactions)
        {
            _relations[factionId] = new Dictionary<string, FactionRelation>();
            foreach (var other in AllFactions)
            {
                if (factionId != other)
                    _relations[factionId][other] = FactionRelation.Neutral;
            }

            _pending[factionId] = new List<PendingProposal>();
        }

        // Set Gorlex (GSC) to war with everyone except IPM, SAW, and CD
        var gorlexAllies = new[] { "IPM", "SAW", "CD" };
        foreach (var factionId in AllFactions)
        {
            if (factionId != "GSC" && !gorlexAllies.Contains(factionId))
            {
                _relations["GSC"][factionId] = FactionRelation.War;
                _relations[factionId]["GSC"] = FactionRelation.War;
            }
        }

        // Set Cyberdon (CD) to war with Shinogara (SHI)
        _relations["CD"]["SHI"] = FactionRelation.War;
        _relations["SHI"]["CD"] = FactionRelation.War;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        var session = args.Session;
        Timer.Spawn(500, () => SendPlayerFaction(session));
    }

    private void SendPlayerFaction(ICommonSession session)
    {
        var faction = GetPlayerFaction(session.AttachedEntity);
        if (faction == null)
            return;

        RaiseNetworkEvent(new PlayerFactionUpdatedEvent(faction), session);
    }

    private void OnUiOpened(EntityUid uid, DiplomacyConsoleComponent comp, BoundUIOpenedEvent args)
    {
        var faction = GetPlayerFaction(args.Actor);
        if (faction == null)
            return;

        SendConsoleState(uid, faction);
    }

    private void OnDeclareWar(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyDeclareWarMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null || myFaction == msg.TargetFactionId)
            return;

        // Check if war is already declared
        if (_relations.TryGetValue(myFaction, out var myRels) &&
            myRels.TryGetValue(msg.TargetFactionId, out var rel) &&
            rel == FactionRelation.War)
            return;

        SetRelation(myFaction, msg.TargetFactionId, FactionRelation.War);

        var fromName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var toName = Loc.GetString($"diplomacy-faction-{msg.TargetFactionId}");
        Announce(Loc.GetString("diplomacy-announce-war", ("faction1", fromName), ("faction2", toName)),
            new Color(1f, 0.27f, 0.27f), _announcer.GetAnnouncementId("diplomacy-war"));

        RefreshAll();
    }

    private void OnProposePeace(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyProposePeaceMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null || myFaction == msg.TargetFactionId)
            return;

        // Check if peace or alliance is already active
        if (_relations.TryGetValue(myFaction, out var myRels) &&
            myRels.TryGetValue(msg.TargetFactionId, out var rel) &&
            (rel == FactionRelation.Peace || rel == FactionRelation.Alliance))
            return;

        if (_pending.TryGetValue(msg.TargetFactionId, out var targetPending) &&
            targetPending.Any(p => p.FromFactionId == myFaction && p.Type == PendingProposalType.Peace))
            return;

        _pending[msg.TargetFactionId].Add(new PendingProposal
        {
            FromFactionId = myFaction,
            ToFactionId = msg.TargetFactionId,
            Type = PendingProposalType.Peace
        });

        var fromName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var toName = Loc.GetString($"diplomacy-faction-{msg.TargetFactionId}");
        Announce(Loc.GetString("diplomacy-announce-peace-proposal", ("from", fromName), ("to", toName)),
            new Color(0.53f, 0.8f, 1f));

        RefreshAll();
    }

    private void OnProposeAlliance(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyProposeAllianceMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null || myFaction == msg.TargetFactionId)
            return;

        // Check if alliance is already active
        if (_relations.TryGetValue(myFaction, out var myRels) &&
            myRels.TryGetValue(msg.TargetFactionId, out var rel) &&
            rel == FactionRelation.Alliance)
            return;

        if (_pending.TryGetValue(msg.TargetFactionId, out var targetPending) &&
            targetPending.Any(p => p.FromFactionId == myFaction && p.Type == PendingProposalType.Alliance))
            return;

        _pending[msg.TargetFactionId].Add(new PendingProposal
        {
            FromFactionId = myFaction,
            ToFactionId = msg.TargetFactionId,
            Type = PendingProposalType.Alliance
        });

        var fromName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var toName = Loc.GetString($"diplomacy-faction-{msg.TargetFactionId}");
        Announce(Loc.GetString("diplomacy-announce-alliance-proposal", ("from", fromName), ("to", toName)),
            new Color(0.53f, 0.8f, 1f));

        RefreshAll();
    }

    private void OnProposeTrade(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyProposeTradeMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null || myFaction == msg.TargetFactionId)
            return;

        // Check if trade is already active
        if (_relations.TryGetValue(myFaction, out var myRels) &&
            myRels.TryGetValue(msg.TargetFactionId, out var rel) &&
            rel == FactionRelation.Trade)
            return;

        if (_pending.TryGetValue(msg.TargetFactionId, out var targetPending) &&
            targetPending.Any(p => p.FromFactionId == myFaction && p.Type == PendingProposalType.Trade))
            return;

        _pending[msg.TargetFactionId].Add(new PendingProposal
        {
            FromFactionId = myFaction,
            ToFactionId = msg.TargetFactionId,
            Type = PendingProposalType.Trade
        });

        var fromName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var toName = Loc.GetString($"diplomacy-faction-{msg.TargetFactionId}");
        Announce(Loc.GetString("diplomacy-announce-trade-proposal", ("from", fromName), ("to", toName)),
            new Color(0.53f, 0.8f, 1f));

        RefreshAll();
    }

    private void OnBreakTrade(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyBreakTradeMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null || myFaction == msg.TargetFactionId)
            return;

        // Check if trade is not active
        if (!_relations.TryGetValue(myFaction, out var myRels) ||
            !myRels.TryGetValue(msg.TargetFactionId, out var rel) ||
            rel != FactionRelation.Trade)
            return;

        SetRelation(myFaction, msg.TargetFactionId, FactionRelation.Neutral);

        var fromName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var toName = Loc.GetString($"diplomacy-faction-{msg.TargetFactionId}");
        Announce(Loc.GetString("diplomacy-announce-trade-broken", ("faction1", fromName), ("faction2", toName)),
            new Color(1f, 0.53f, 0.27f));

        RefreshAll();
    }

    private void OnAcceptProposal(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyAcceptProposalMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null)
            return;

        if (!_pending.TryGetValue(myFaction, out var myPending))
            return;

        var proposal = myPending.FirstOrDefault(p =>
            p.FromFactionId == msg.FromFactionId && p.Type == msg.Type);

        if (proposal == null)
            return;

        myPending.Remove(proposal);

        var newRelation = msg.Type switch
        {
            PendingProposalType.Peace => FactionRelation.Peace,
            PendingProposalType.Alliance => FactionRelation.Alliance,
            PendingProposalType.Trade => FactionRelation.Trade,
            _ => FactionRelation.Neutral
        };
        SetRelation(myFaction, proposal.FromFactionId, newRelation);

        var fromName = Loc.GetString($"diplomacy-faction-{proposal.FromFactionId}");
        var toName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var (key, soundId) = msg.Type switch
        {
            PendingProposalType.Peace => ("diplomacy-announce-peace-accepted", _announcer.GetAnnouncementId("diplomacy-peace")),
            PendingProposalType.Alliance => ("diplomacy-announce-alliance-accepted", _announcer.GetAnnouncementId("diplomacy-alliance")),
            PendingProposalType.Trade => ("diplomacy-announce-trade-accepted", null),
            _ => (null, null)
        };
        if (key != null)
            Announce(Loc.GetString(key, ("faction1", fromName), ("faction2", toName)),
                new Color(0.27f, 1f, 0.53f), soundId);

        RefreshAll();
    }

    private void OnRejectProposal(EntityUid uid, DiplomacyConsoleComponent comp, DiplomacyRejectProposalMessage msg)
    {
        var myFaction = GetPlayerFaction(msg.Actor);
        if (myFaction == null)
            return;

        if (!_pending.TryGetValue(myFaction, out var myPending))
            return;

        var proposal = myPending.FirstOrDefault(p =>
            p.FromFactionId == msg.FromFactionId && p.Type == msg.Type);

        if (proposal == null)
            return;

        myPending.Remove(proposal);

        var fromName = Loc.GetString($"diplomacy-faction-{proposal.FromFactionId}");
        var toName = Loc.GetString($"diplomacy-faction-{myFaction}");
        var key = msg.Type switch
        {
            PendingProposalType.Peace => "diplomacy-announce-peace-rejected",
            PendingProposalType.Alliance => "diplomacy-announce-alliance-rejected",
            PendingProposalType.Trade => "diplomacy-announce-trade-rejected",
            _ => null
        };
        if (key != null)
            Announce(Loc.GetString(key, ("from", fromName), ("to", toName)),
                new Color(1f, 0.53f, 0.27f));

        RefreshAll();
    }

    private void SetRelation(string f1, string f2, FactionRelation rel)
    {
        if (!_relations.ContainsKey(f1)) _relations[f1] = new();
        if (!_relations.ContainsKey(f2)) _relations[f2] = new();
        _relations[f1][f2] = rel;
        _relations[f2][f1] = rel;
    }

    private string? GetPlayerFaction(EntityUid? entity)
    {
        if (entity == null || !entity.Value.Valid)
            return null;
        return TryComp<HullrotFactionComponent>(entity.Value, out var factionComp)
            ? factionComp.Faction
            : null;
    }

    private void Announce(string msg, Color color, string? announcementId = null)
    {
        if (announcementId != null)
        {
            _announcer.SendAnnouncement(announcementId, Filter.Broadcast(), msg,
                sender: Loc.GetString("diplomacy-announcer-name"), colorOverride: color);
        }
        else
        {
            _announcer.SendAnnouncementMessage("fallback", msg, sender: Loc.GetString("diplomacy-announcer-name"), colorOverride: color);
        }
    }

    private void RefreshAll()
    {
        BroadcastRelations();

        foreach (var session in _playerManager.Sessions)
        {
            var faction = GetPlayerFaction(session.AttachedEntity);
            if (faction != null)
                RaiseNetworkEvent(new PlayerFactionUpdatedEvent(faction), session);
        }

        var consoleQuery = EntityQueryEnumerator<DiplomacyConsoleComponent>();
        while (consoleQuery.MoveNext(out var uid, out _))
        {
            var actors = _ui.GetActors(uid, DiplomacyConsoleUiKey.Key);
            foreach (var actorUid in actors)
            {
                var faction = GetPlayerFaction(actorUid);
                if (faction != null)
                    SendConsoleState(uid, faction);
            }
        }
    }

    private void SendConsoleState(EntityUid consoleUid, string faction)
    {
        var relations = _relations.GetValueOrDefault(faction, new Dictionary<string, FactionRelation>());
        var pending = _pending.GetValueOrDefault(faction, new List<PendingProposal>());

        _ui.SetUiState(consoleUid, DiplomacyConsoleUiKey.Key,
            new DiplomacyConsoleBoundUserInterfaceState(relations, faction, pending));
    }

    private void BroadcastRelations()
    {
        var ev = new AllFactionRelationsUpdatedEvent(
            _relations.ToDictionary(kvp => kvp.Key, kvp => new Dictionary<string, FactionRelation>(kvp.Value)),
            _pending.ToDictionary(kvp => kvp.Key, kvp => new List<PendingProposal>(kvp.Value)));

        RaiseNetworkEvent(ev, Filter.Broadcast(), false);
    }
}
