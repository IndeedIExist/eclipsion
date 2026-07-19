using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server._Crescent.Diplomacy;
using Content.Server.Power.Components;
using Content.Shared._Crescent.Diplomacy;
using Content.Shared._Crescent.RoundEnd;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// The conquest win condition. Every faction that fields a <see cref="FactionStationComponent"/> station stays in
/// the war until all of its stations fall — a station falls when its grid is gone, or when it has sat without any
/// powered APC for <see cref="FactionConquestRuleComponent.BlackoutToFall"/>. Each station broadcasts its own
/// obituary the moment it goes dark.
///
/// Who wins:
///  * exactly one great power left standing (with any number of allies) — that power takes it;
///  * both great powers dead — the minors do NOT have to finish each other off, every survivor shares the sector;
///  * both great powers still alive — nothing is settled, the war continues.
///
/// A settled war does not end the round straight away: <see cref="FactionConquestRuleComponent.VictoryDelay"/>
/// gives whoever is left one last window to move, and restoring a station's power calls the whole thing off.
/// If the round instead runs out of time, the surviving great power is credited; failing that, Taypan is swallowed.
/// </summary>
public sealed class FactionConquestRuleSystem : GameRuleSystem<FactionConquestRuleComponent>
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DiplomacySystem _diplomacy = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Started(EntityUid uid, FactionConquestRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextCheck = _timing.CurTime + component.CheckInterval;
    }

    /// <summary>
    /// The round ended without the conquest resolving — almost always the 4h cap. A great power still standing
    /// takes the sector by default; if none is, nobody won and the Abyss takes it.
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid, FactionConquestRuleComponent conquest,
        GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, conquest, gameRule, ref args);

        if (!GameTicker.IsGameRuleActive(uid, gameRule) || conquest.Decided)
            return;

        conquest.Decided = true;

        var alive = GetSurvivingFactions(conquest);
        var majors = alive.Where(f => conquest.MajorFactions.Contains(f)).ToList();

        if (majors.Count == 1 && conquest.VictoryAnnouncements.TryGetValue(majors[0], out var victory))
            _chat.DispatchServerAnnouncement(Loc.GetString(victory));
        else
            _chat.DispatchServerAnnouncement(Loc.GetString(conquest.TimeoutAnnouncement));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FactionConquestRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var conquest, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule) || conquest.Decided)
                continue;

            if (_timing.CurTime < conquest.NextCheck)
                continue;

            conquest.NextCheck = _timing.CurTime + conquest.CheckInterval;
            Evaluate(conquest);
        }
    }

    private void Evaluate(FactionConquestRuleComponent conquest)
    {
        var alive = GetSurvivingFactions(conquest);

        // Nobody holds anything — there is no victor to crown.
        if (alive.Count == 0)
        {
            conquest.PendingWinners = null;
            return;
        }

        if (!TryResolveWinners(conquest, alive, out var winners))
        {
            // The war reopened (power restored, alliance broken) — call off any pending victory.
            conquest.PendingWinners = null;
            return;
        }

        // First time this looks settled: start the response window rather than ending immediately.
        if (conquest.PendingWinners == null || !conquest.PendingWinners.SequenceEqual(winners))
        {
            conquest.PendingWinners = winners;
            conquest.PendingSince = _timing.CurTime;
            return;
        }

        if (_timing.CurTime - conquest.PendingSince < conquest.VictoryDelay)
            return;

        Declare(conquest, winners);
    }

    private void Declare(FactionConquestRuleComponent conquest, List<string> winners)
    {
        conquest.Decided = true;

        // A lone great power gets its own ending; surviving minors share a generic one that names nobody.
        if (winners.Count == 1 && conquest.VictoryAnnouncements.TryGetValue(winners[0], out var victory))
            _chat.DispatchServerAnnouncement(Loc.GetString(victory));
        else
            _chat.DispatchServerAnnouncement(Loc.GetString(conquest.MinorVictoryAnnouncement));

        GameTicker.EndRound($"{string.Join(", ", winners)} won the war for Taypan.");
        Timer.Spawn(conquest.RestartDelay, () => GameTicker.RestartRound());
    }

    /// <summary>Factions that still hold at least one standing station. Announces stations as they go dark.</summary>
    private HashSet<string> GetSurvivingFactions(FactionConquestRuleComponent conquest)
    {
        var alive = new HashSet<string>();
        var seen = new HashSet<EntityUid>();
        var powered = GetPoweredGrids();

        var query = EntityQueryEnumerator<FactionStationComponent>();
        while (query.MoveNext(out var stationUid, out var station))
        {
            seen.Add(stationUid);

            if (powered.Contains(stationUid))
            {
                // Lights are back on; the station is in the war again.
                conquest.DarkSince.Remove(stationUid);
                conquest.AnnouncedFallen.Remove(stationUid);
                alive.Add(station.Faction);
                continue;
            }

            if (!conquest.DarkSince.TryGetValue(stationUid, out var since))
            {
                conquest.DarkSince[stationUid] = _timing.CurTime;
                alive.Add(station.Faction);
                continue;
            }

            // Dark, but not long enough to count as lost yet.
            if (_timing.CurTime - since < conquest.BlackoutToFall)
            {
                alive.Add(station.Faction);
                continue;
            }

            AnnounceFall(conquest, stationUid, station);
        }

        // Stations that no longer exist stop being tracked (a destroyed grid is simply gone from the query).
        foreach (var tracked in conquest.DarkSince.Keys.ToList())
        {
            if (!seen.Contains(tracked))
                conquest.DarkSince.Remove(tracked);
        }

        return alive;
    }

    private void AnnounceFall(FactionConquestRuleComponent conquest, EntityUid stationUid, FactionStationComponent station)
    {
        if (station.FallAnnouncement is not { } message || !conquest.AnnouncedFallen.Add(stationUid))
            return;

        _chat.DispatchServerAnnouncement(Loc.GetString(message));
    }

    /// <summary>
    /// Every grid that still has a charged APC on it, gathered in one sweep. A station whose APCs have all run
    /// flat is dark — that is the signal we treat as "the power is gone".
    /// </summary>
    private HashSet<EntityUid> GetPoweredGrids()
    {
        var powered = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<ApcComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var battery, out var xform))
        {
            if (battery.CurrentCharge > 0f && xform.GridUid is { } grid)
                powered.Add(grid);
        }

        return powered;
    }

    /// <summary>Decides whether the war is settled, and who is credited for it.</summary>
    private bool TryResolveWinners(FactionConquestRuleComponent conquest, HashSet<string> alive, out List<string> winners)
    {
        winners = new List<string>();

        var majors = alive.Where(f => conquest.MajorFactions.Contains(f)).ToList();

        switch (majors.Count)
        {
            // Both great powers are gone. The minors are not made to finish each other off — whoever is still
            // standing inherits Taypan together, allied or not.
            case 0:
                winners = alive.OrderBy(f => f).ToList();
                return true;

            // One great power left: it wins provided everyone else standing is allied to it.
            case 1:
                foreach (var faction in alive)
                {
                    if (faction == majors[0])
                        continue;

                    if (_diplomacy.GetRelations(majors[0], faction) != Relations.Ally)
                        return false;
                }

                winners.Add(majors[0]);
                return true;

            // Two great powers still standing is not a settled war, allied or not.
            default:
                return false;
        }
    }
}
