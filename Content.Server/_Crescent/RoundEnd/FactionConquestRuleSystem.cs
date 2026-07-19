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
/// The conquest win condition. A faction is counted as holding the sector for as long as a station of its own is
/// standing — a station falls when its grid is gone, or when it has sat without any powered APC for
/// <see cref="FactionConquestRuleComponent.BlackoutToFall"/>. Losing power is announced when the clock starts, and
/// each station broadcasts its own obituary once the clock runs out.
///
/// This is pure scorekeeping: nobody is ever taken out of the war, and diplomacy is never touched. DSM and NCWL
/// are at war by definition and stay that way whatever happens to their hulls — losing Aurora or Balreska only
/// means that side no longer holds a seat of power, not that it has bowed out of anything.
///
/// Who wins:
///  * exactly one great power left standing — that power takes it, whoever else is still holding a station;
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

        if (!GameTicker.IsGameRuleActive(uid, gameRule))
            return;

        // The war was already called mid-round; just restate it on the summary screen.
        if (conquest.Decided)
        {
            AppendSummary(conquest, ref args);
            return;
        }

        conquest.Decided = true;

        var alive = GetSurvivingFactions(conquest);
        var majors = alive.Where(f => conquest.MajorFactions.Contains(f)).ToList();

        if (majors.Count == 1 && conquest.VictoryAnnouncements.TryGetValue(majors[0], out var victory))
        {
            conquest.Winners = majors;
            _chat.DispatchServerAnnouncement(Loc.GetString(victory));
        }
        else
        {
            _chat.DispatchServerAnnouncement(Loc.GetString(conquest.TimeoutAnnouncement));
        }

        AppendSummary(conquest, ref args);
    }

    /// <summary>Who took Taypan and which seats of power were left standing, for the round end screen.</summary>
    private void AppendSummary(FactionConquestRuleComponent conquest, ref RoundEndTextAppendEvent args)
    {
        args.AddLine(conquest.Winners.Count > 0
            ? Loc.GetString("faction-conquest-summary-winner", ("factions", string.Join(", ", conquest.Winners)))
            : Loc.GetString("faction-conquest-summary-nobody"));

        var query = EntityQueryEnumerator<FactionStationComponent>();
        while (query.MoveNext(out var stationUid, out var station))
        {
            args.AddLine(Loc.GetString(
                conquest.AnnouncedFallen.Contains(stationUid)
                    ? "faction-conquest-summary-station-fallen"
                    : "faction-conquest-summary-station-standing",
                ("station", station.StationName),
                ("faction", station.Faction)));
        }
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
            CancelPending(conquest);
            return;
        }

        if (!TryResolveWinners(conquest, alive, out var winners))
        {
            // The war reopened (power restored, alliance broken) — call off any pending victory.
            CancelPending(conquest);
            return;
        }

        // First time this looks settled: start the response window rather than ending immediately.
        if (conquest.PendingWinners == null || !conquest.PendingWinners.SequenceEqual(winners))
        {
            conquest.PendingWinners = winners;
            conquest.PendingSince = _timing.CurTime;

            // Tell the sector, or the response window is a silent countdown nobody can respond to.
            _chat.DispatchServerAnnouncement(Loc.GetString(conquest.PendingAnnouncement,
                ("factions", string.Join(", ", winners)),
                ("minutes", (int) conquest.VictoryDelay.TotalMinutes)));
            return;
        }

        if (_timing.CurTime - conquest.PendingSince < conquest.VictoryDelay)
            return;

        Declare(conquest, winners);
    }

    /// <summary>Drops a pending victory, telling the sector about it if one was actually running.</summary>
    private void CancelPending(FactionConquestRuleComponent conquest)
    {
        if (conquest.PendingWinners == null)
            return;

        conquest.PendingWinners = null;
        _chat.DispatchServerAnnouncement(Loc.GetString(conquest.PendingCancelledAnnouncement));
    }

    private void Declare(FactionConquestRuleComponent conquest, List<string> winners)
    {
        conquest.Decided = true;
        conquest.Winners = winners;

        // A lone great power gets its own ending; surviving minors share a generic one that names nobody.
        if (winners.Count == 1 && conquest.VictoryAnnouncements.TryGetValue(winners[0], out var victory))
            _chat.DispatchServerAnnouncement(Loc.GetString(victory));
        else
            _chat.DispatchServerAnnouncement(Loc.GetString(conquest.MinorVictoryAnnouncement));

        GameTicker.EndRound($"{string.Join(", ", winners)} won the war for Taypan.");
        Timer.Spawn(conquest.RestartDelay, () => GameTicker.RestartRound());
    }

    /// <summary>
    /// Factions that still hold a standing station, announcing stations as they go dark. A faction dropping out of
    /// this set has only lost its seat of power — it is not out of the war, and its diplomacy is untouched.
    /// </summary>
    private HashSet<string> GetSurvivingFactions(FactionConquestRuleComponent conquest)
    {
        var alive = new HashSet<string>();
        var seen = new HashSet<EntityUid>();
        var powered = GetPoweredGrids();

        var query = EntityQueryEnumerator<FactionStationComponent>();
        while (query.MoveNext(out var stationUid, out var station))
        {
            seen.Add(stationUid);
            conquest.KnownStations[stationUid] = station.FallAnnouncement ?? string.Empty;

            if (powered.Contains(stationUid))
            {
                // Lights are back on; the station is in the war again.
                conquest.EverPowered.Add(stationUid);
                conquest.DarkSince.Remove(stationUid);
                conquest.AnnouncedFallen.Remove(stationUid);
                conquest.AnnouncedBlackout.Remove(stationUid);
                alive.Add(station.Faction);
                continue;
            }

            // Still booting: mapped APCs are saved flat, so a station is "dark" for the first seconds of every
            // round. Nothing counts against it until it has been powered once.
            if (!conquest.EverPowered.Contains(stationUid))
            {
                alive.Add(station.Faction);
                continue;
            }

            if (!conquest.DarkSince.TryGetValue(stationUid, out var since))
            {
                // The blackout clock starts now. Say so — the defenders get a chance to answer it, and the
                // attackers learn their push actually landed.
                conquest.DarkSince[stationUid] = _timing.CurTime;
                AnnounceBlackout(conquest, stationUid, station);
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

        // A station that is outright destroyed never gets to be seen "dark" — it simply vanishes from the query.
        // Catch it here so a blown-apart hull still gets its obituary instead of dying silently.
        foreach (var (tracked, obituary) in conquest.KnownStations.ToList())
        {
            if (seen.Contains(tracked))
                continue;

            if (!string.IsNullOrEmpty(obituary) && conquest.AnnouncedFallen.Add(tracked))
                _chat.DispatchServerAnnouncement(Loc.GetString(obituary));

            conquest.KnownStations.Remove(tracked);
            conquest.DarkSince.Remove(tracked);
        }

        return alive;
    }

    /// <summary>
    /// Warns the sector that a station has gone dark and is now on the clock. Announced once per blackout —
    /// power coming back clears the flag, so a station that is knocked out twice is reported twice.
    /// </summary>
    private void AnnounceBlackout(FactionConquestRuleComponent conquest, EntityUid stationUid, FactionStationComponent station)
    {
        if (!conquest.AnnouncedBlackout.Add(stationUid))
            return;

        _chat.DispatchServerAnnouncement(Loc.GetString(conquest.BlackoutAnnouncement,
            ("station", station.StationName),
            ("faction", station.Faction),
            ("minutes", (int) conquest.BlackoutToFall.TotalMinutes)));
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
            // Both great powers have lost their seats. The minors are not made to finish each other off — whoever
            // is still holding a station inherits Taypan together, allied or not.
            case 0:
                winners = alive.OrderBy(f => f).ToList();
                return true;

            // One great power still holds a seat of power, so the war between the great powers has an answer —
            // and that is the war being fought. Surviving minors do not block it: they never had a throne to lose,
            // and holding Tatsumoto or Jackal to the end would otherwise stall every round out to the time cap.
            case 1:
                winners.Add(majors[0]);
                return true;

            // Two great powers still standing is not a settled war, allied or not.
            default:
                return false;
        }
    }
}
