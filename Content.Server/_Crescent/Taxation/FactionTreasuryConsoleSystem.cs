using Content.Server.Crescent.Dispenser;
using Content.Server._Crescent.Overwatch;
using Content.Server.Stack;
using Content.Shared._Crescent.Taxation;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// Backs the faction treasury console: authorized faction members view/withdraw accumulated tax
/// revenue and deposit physical cash. Anyone without access cannot open the UI; instead their click
/// starts (or reports on) a robbery that siphons a fixed share of the vault over several minutes and
/// then stops, so defenders can respond and the vault is never drained instantly.
/// </summary>
public sealed class FactionTreasuryConsoleSystem : EntitySystem
{
    [Dependency] private readonly StationTradeMarketSystem _market = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly OverwatchSystem _overwatch = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionTreasuryConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, TreasuryWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, InteractUsingEvent>(OnInteractUsing);
    }

    /// <summary>
    /// On round start, tie this station's treasury to the console's faction and load its persisted,
    /// cross-round balance so the vault no longer resets to zero each round.
    /// </summary>
    private void OnConsoleMapInit(EntityUid uid, FactionTreasuryConsoleComponent comp, MapInitEvent args)
    {
        if (string.IsNullOrEmpty(comp.Faction))
            return;

        var station = _market.TryGetOwningStation(uid);
        if (station is null)
            return;

        _market.BindFactionTreasury(station.Value, comp.Faction);
        UpdateUi(uid);
    }

    /// <summary>
    /// Gate the UI: without faction funds access the console won't open at all. Trying anyway
    /// pops a warning and sounds the intrusion alarm (rate-limited so it can't be spammed).
    /// </summary>
    private void OnOpenAttempt(EntityUid uid, FactionTreasuryConsoleComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_access.IsAllowed(args.User, uid))
            return;

        _popup.PopupEntity(Loc.GetString("treasury-console-intrusion"), uid, args.User, PopupType.MediumCaution);
        TriggerRobbery(uid, comp, args.User);
        args.Cancel();
    }

    /// <summary>
    /// A thief's unauthorized click. If the vault is idle, starts a fresh heist that siphons
    /// <see cref="FactionTreasuryConsoleComponent.RobberyFraction"/> of the current balance over
    /// <see cref="FactionTreasuryConsoleComponent.RobberyDuration"/>, then stops on its own. If a
    /// robbery is already running, just reports progress to the robber (their "screen"/log feedback).
    /// Clicking again after one finishes starts another share.
    /// </summary>
    private void TriggerRobbery(EntityUid uid, FactionTreasuryConsoleComponent comp, EntityUid user)
    {
        var now = _timing.CurTime;

        SoundAlarm(uid, comp);
        AnnounceIntrusion(uid, comp);

        // Already robbing? Report status instead of stacking another heist on top.
        if (comp.RobberyStart is not null && comp.RobberyEnd is { } activeEnd && now < activeEnd)
        {
            var remainingCr = Math.Max(0, comp.RobberyTotal - comp.RobberyDispensed);
            var minsLeft = Math.Max(1, (int) Math.Ceiling((activeEnd - now).TotalMinutes));
            _popup.PopupEntity(
                Loc.GetString("treasury-console-robbery-progress",
                    ("stolen", comp.RobberyDispensed), ("remaining", remainingCr), ("minutes", minsLeft)),
                uid, user, PopupType.MediumCaution);
            return;
        }

        var station = _market.TryGetOwningStation(uid);
        var balance = station is null ? 0 : _market.GetTreasury(station.Value);

        var target = (int) (balance * comp.RobberyFraction);
        if (target <= 0)
        {
            _popup.PopupEntity(Loc.GetString("treasury-console-robbery-empty"), uid, user, PopupType.MediumCaution);
            return;
        }

        comp.RobberyTotal = target;
        comp.RobberyDispensed = 0;
        comp.RobberyStart = now;
        comp.RobberyEnd = now + comp.RobberyDuration;
        comp.LastLoot = now;

        var mins = Math.Max(1, (int) Math.Round(comp.RobberyDuration.TotalMinutes));
        var pct = (int) MathF.Round(comp.RobberyFraction * 100f);
        _popup.PopupEntity(
            Loc.GetString("treasury-console-robbery-started",
                ("amount", target), ("percent", pct), ("minutes", mins)),
            uid, user, PopupType.LargeCaution);

        UpdateUi(uid);
    }

    /// <summary>Plays the local intrusion alarm, throttled by <see cref="FactionTreasuryConsoleComponent.AlarmInterval"/> so it never spams.</summary>
    private void SoundAlarm(EntityUid uid, FactionTreasuryConsoleComponent comp)
    {
        var now = _timing.CurTime;
        if (comp.LastAlarm is { } last && now - last < comp.AlarmInterval)
            return;

        comp.LastAlarm = now;
        _audio.PlayPvs(comp.AlarmSound, uid);
    }

    /// <summary>
    /// Fires a faction-only overwatch alert naming the station (throttled by
    /// <see cref="FactionTreasuryConsoleComponent.AnnounceCooldown"/>). Only the vault's faction sees it.
    /// </summary>
    private void AnnounceIntrusion(EntityUid uid, FactionTreasuryConsoleComponent comp)
    {
        if (string.IsNullOrEmpty(comp.Faction))
            return;

        var now = _timing.CurTime;
        if (comp.LastAnnounce is { } lastAnnounce && now - lastAnnounce < comp.AnnounceCooldown)
            return;

        comp.LastAnnounce = now;

        // Name the station in the alert for roleplay flavour (e.g. "the Aurora treasury vault").
        var station = _market.TryGetOwningStation(uid);
        var stationName = station is null ? Loc.GetString("treasury-console-alarm-unknown-station") : MetaData(station.Value).EntityName;

        _overwatch.SendFactionAnnouncement(
            comp.Faction,
            Loc.GetString("treasury-console-alarm-announcement", ("station", stationName)));
    }

    /// <summary>Depositing physical cash (SpaceCash) straight into the faction treasury.</summary>
    private void OnInteractUsing(EntityUid uid, FactionTreasuryConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only cash stacks may be deposited.
        if (!TryComp<StackComponent>(args.Used, out var stack)
            || MetaData(args.Used).EntityPrototype?.ID != "SpaceCash")
        {
            return;
        }

        args.Handled = true;

        if (!_access.IsAllowed(args.User, uid))
        {
            _popup.PopupEntity(Loc.GetString("treasury-console-deposit-denied"), uid, args.User, PopupType.MediumCaution);
            SoundAlarm(uid, comp);
            AnnounceIntrusion(uid, comp);
            return;
        }

        var station = _market.TryGetOwningStation(uid);
        if (station is null || stack.Count <= 0)
            return;

        var amount = stack.Count;
        _market.AddTreasury(station.Value, amount);
        QueueDel(args.Used);

        // An authorized member operating the console re-secures a breach in progress.
        Resecure(comp);

        _popup.PopupEntity(Loc.GetString("treasury-console-deposited", ("amount", amount)), uid, args.User, PopupType.Medium);
        UpdateUi(uid);
    }

    private void OnOpened(EntityUid uid, FactionTreasuryConsoleComponent comp, BoundUIOpenedEvent args)
    {
        // OnOpenAttempt already denied anyone without access, so reaching here means authorized:
        // an authorized member at the console re-secures any breach in progress.
        Resecure(comp);
        UpdateUi(uid);
    }

    private void OnWithdraw(EntityUid uid, FactionTreasuryConsoleComponent comp, TreasuryWithdrawMessage args)
    {
        if (!_access.IsAllowed(args.Actor, uid))
            return;

        // An authorized member operating the console re-secures a breach in progress.
        Resecure(comp);

        var station = _market.TryGetOwningStation(uid);
        if (station is null)
            return;

        var amount = Math.Max(0, args.Amount);
        if (amount == 0)
            return;

        // Identify the withdrawing player so we can enforce the per-person, per-round cap.
        if (!TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        var withdrawn = _market.TryWithdrawTreasuryCapped(
            station.Value, actor.PlayerSession.UserId, amount, comp.MaxWithdrawFraction);

        if (withdrawn > 0)
        {
            _stack.SpawnMultiple("SpaceCash", withdrawn, Transform(uid).Coordinates);
            _popup.PopupEntity(
                Loc.GetString("treasury-console-withdrew", ("amount", withdrawn)),
                uid, args.Actor, PopupType.Medium);
        }
        else
        {
            // Either the vault is empty or this member has already hit their round withdrawal cap.
            _popup.PopupEntity(
                Loc.GetString("treasury-console-limit-reached",
                    ("percent", (int) MathF.Round(comp.MaxWithdrawFraction * 100f))),
                uid, args.Actor, PopupType.MediumCaution);
        }

        UpdateUi(uid);
    }

    /// <summary>
    /// Pays out active robberies: each running heist spills its captured share as physical cash next
    /// to the console, spread evenly over <see cref="FactionTreasuryConsoleComponent.RobberyDuration"/>,
    /// then stops on its own. An authorized member operating the console (or an empty vault) ends it early.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FactionTreasuryConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.RobberyStart is not { } start || comp.RobberyEnd is not { } end)
                continue;

            var finished = now >= end;

            // Pace the payout; skip until the next tick unless we're wrapping up.
            if (!finished && comp.LastLoot is { } last && now - last < comp.RobberyTickInterval)
                continue;

            comp.LastLoot = now;

            var station = _market.TryGetOwningStation(uid);
            if (station is not null)
            {
                // How much of the captured total should have been paid out by now (linear over the duration).
                var progress = finished
                    ? 1.0
                    : Math.Clamp((now - start).TotalSeconds / comp.RobberyDuration.TotalSeconds, 0.0, 1.0);
                var due = (int) (comp.RobberyTotal * progress) - comp.RobberyDispensed;

                if (due > 0)
                {
                    var stolen = _market.TryWithdrawTreasury(station.Value, due);
                    if (stolen > 0)
                    {
                        comp.RobberyDispensed += stolen;
                        _stack.SpawnMultiple("SpaceCash", stolen, Transform(uid).Coordinates);
                        _popup.PopupEntity(Loc.GetString("treasury-console-looted", ("amount", stolen)), uid, PopupType.LargeCaution);
                        UpdateUi(uid);
                    }
                }

                SoundAlarm(uid, comp);
            }

            if (finished)
                StopRobbery(comp);
        }
    }

    /// <summary>Clears any active robbery: it has finished paying out, or an authorized member re-secured the console.</summary>
    private void StopRobbery(FactionTreasuryConsoleComponent comp)
    {
        comp.RobberyStart = null;
        comp.RobberyEnd = null;
        comp.RobberyTotal = 0;
        comp.RobberyDispensed = 0;
        comp.LastLoot = null;
    }

    /// <summary>An authorized member has taken control of the console, halting any robbery in progress.</summary>
    private void Resecure(FactionTreasuryConsoleComponent comp)
    {
        StopRobbery(comp);
    }

    private void UpdateUi(EntityUid uid)
    {
        var station = _market.TryGetOwningStation(uid);
        var balance = station is null ? 0 : _market.GetTreasury(station.Value);
        var robbed = TryComp<FactionTreasuryConsoleComponent>(uid, out var comp) && comp.RobberyStart != null;

        // Only authorized members can have the UI open, so authorized is always true here.
        _ui.SetUiState(uid, TreasuryConsoleUiKey.Key, new TreasuryConsoleState(balance, true, robbed));
    }
}
