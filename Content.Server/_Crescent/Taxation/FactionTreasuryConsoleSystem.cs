using Content.Server.Crescent.Dispenser;
using Content.Server._Rat.Overwatch;
using Content.Server.Stack;
using Content.Shared._Crescent.Taxation;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// Backs the faction treasury console: authorized faction members view/withdraw accumulated tax
/// revenue and deposit physical cash. Anyone without access cannot open the UI at all; the attempt
/// is blocked and a rate-limited intrusion alarm sounds instead.
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

        SubscribeLocalEvent<FactionTreasuryConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, TreasuryWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, InteractUsingEvent>(OnInteractUsing);
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
        RaiseIntrusion(uid, comp);
        args.Cancel();
    }

    /// <summary>
    /// Responds to unauthorized tampering: a local alarm (throttled by <see cref="FactionTreasuryConsoleComponent.AlarmCooldown"/>)
    /// and a faction-only overwatch alert naming the station (throttled by <see cref="FactionTreasuryConsoleComponent.AnnounceCooldown"/>).
    /// Only members of the vault's faction see the alert.
    /// </summary>
    private void RaiseIntrusion(EntityUid uid, FactionTreasuryConsoleComponent comp)
    {
        var now = _timing.CurTime;

        if (comp.LastAlarm is not { } lastAlarm || now - lastAlarm >= comp.AlarmCooldown)
        {
            comp.LastAlarm = now;
            _audio.PlayPvs(comp.AlarmSound, uid);
        }

        if (string.IsNullOrEmpty(comp.Faction))
            return;

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
            RaiseIntrusion(uid, comp);
            return;
        }

        var station = _market.TryGetOwningStation(uid);
        if (station is null || stack.Count <= 0)
            return;

        var amount = stack.Count;
        _market.AddTreasury(station.Value, amount);
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("treasury-console-deposited", ("amount", amount)), uid, args.User, PopupType.Medium);
        UpdateUi(uid);
    }

    private void OnOpened(EntityUid uid, FactionTreasuryConsoleComponent comp, BoundUIOpenedEvent args)
    {
        // OnOpenAttempt already denied anyone without access, so reaching here means authorized.
        UpdateUi(uid);
    }

    private void OnWithdraw(EntityUid uid, FactionTreasuryConsoleComponent comp, TreasuryWithdrawMessage args)
    {
        if (!_access.IsAllowed(args.Actor, uid))
            return;

        var station = _market.TryGetOwningStation(uid);
        if (station is null)
            return;

        var amount = Math.Max(0, args.Amount);
        if (amount == 0)
            return;

        var withdrawn = _market.TryWithdrawTreasury(station.Value, amount);
        if (withdrawn > 0)
        {
            _stack.SpawnMultiple("SpaceCash", withdrawn, Transform(uid).Coordinates);
            _popup.PopupEntity(
                Loc.GetString("treasury-console-withdrew", ("amount", withdrawn)),
                uid, args.Actor, PopupType.Medium);
        }

        UpdateUi(uid);
    }

    private void UpdateUi(EntityUid uid)
    {
        var station = _market.TryGetOwningStation(uid);
        var balance = station is null ? 0 : _market.GetTreasury(station.Value);

        // Only authorized members can have the UI open, so authorized is always true here.
        _ui.SetUiState(uid, TreasuryConsoleUiKey.Key, new TreasuryConsoleState(balance, true, false));
    }
}
