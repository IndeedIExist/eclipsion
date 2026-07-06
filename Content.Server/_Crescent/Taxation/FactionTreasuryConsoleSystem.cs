using Content.Server.Crescent.Dispenser;
using Content.Server.Stack;
using Content.Shared._Crescent.Taxation;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// Backs the faction treasury console: viewing/withdrawing accumulated tax revenue and the
/// anti-theft security response when an unauthorized person tampers with it.
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionTreasuryConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, TreasuryWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<FactionTreasuryConsoleComponent, InteractUsingEvent>(OnInteractUsing);
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
            return;
        }

        var station = _market.TryGetOwningStation(uid);
        if (station is null || stack.Count <= 0)
            return;

        var amount = stack.Count;
        _market.AddTreasury(station.Value, amount);
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("treasury-console-deposited", ("amount", amount)), uid, args.User, PopupType.Medium);
        UpdateUi(uid, comp, true);
    }

    private void OnOpened(EntityUid uid, FactionTreasuryConsoleComponent comp, BoundUIOpenedEvent args)
    {
        var authorized = _access.IsAllowed(args.Actor, uid);

        if (authorized)
            SecureConsole(uid, comp);
        else
            TriggerBreach(uid, comp, args.Actor);

        UpdateUi(uid, comp, authorized);
    }

    private void OnWithdraw(EntityUid uid, FactionTreasuryConsoleComponent comp, TreasuryWithdrawMessage args)
    {
        if (!_access.IsAllowed(args.Actor, uid))
        {
            TriggerBreach(uid, comp, args.Actor);
            UpdateUi(uid, comp, false);
            return;
        }

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

        UpdateUi(uid, comp, true);
    }

    /// <summary>An authorized member re-secures the console, ending any active breach.</summary>
    private void SecureConsole(EntityUid uid, FactionTreasuryConsoleComponent comp)
    {
        if (!comp.AlarmActive)
            return;

        comp.AlarmActive = false;
        comp.NextTheft = null;
    }

    /// <summary>An unauthorized person tampered with the console: raise the alarm and arm the theft timer.</summary>
    private void TriggerBreach(EntityUid uid, FactionTreasuryConsoleComponent comp, EntityUid actor)
    {
        _popup.PopupEntity(Loc.GetString("treasury-console-intrusion"), uid, actor, PopupType.LargeCaution);

        if (comp.AlarmActive)
            return;

        comp.AlarmActive = true;
        comp.NextTheft = _timing.CurTime + comp.IntrusionDelay;
        _audio.PlayPvs(comp.AlarmSound, uid);
    }

    private void UpdateUi(EntityUid uid, FactionTreasuryConsoleComponent comp, bool authorized)
    {
        var station = _market.TryGetOwningStation(uid);
        var balance = station is null ? 0 : _market.GetTreasury(station.Value);

        _ui.SetUiState(uid, TreasuryConsoleUiKey.Key, new TreasuryConsoleState(balance, authorized, comp.AlarmActive));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FactionTreasuryConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.AlarmActive || comp.NextTheft is null || now < comp.NextTheft.Value)
                continue;

            LootTreasury(uid, comp);

            // Keep draining on the same cadence until an authorized member secures the console.
            comp.NextTheft = now + comp.IntrusionDelay;
        }
    }

    /// <summary>Siphons a chunk of the treasury as physical cash at the console.</summary>
    private void LootTreasury(EntityUid uid, FactionTreasuryConsoleComponent comp)
    {
        var station = _market.TryGetOwningStation(uid);
        if (station is null)
            return;

        var stolen = _market.TryWithdrawTreasury(station.Value, comp.TheftAmount);

        _audio.PlayPvs(comp.AlarmSound, uid);

        if (stolen > 0)
        {
            _stack.SpawnMultiple("SpaceCash", stolen, Transform(uid).Coordinates);
            _popup.PopupEntity(Loc.GetString("treasury-console-looted", ("amount", stolen)), uid, PopupType.LargeCaution);
        }

        UpdateUi(uid, comp, false);
    }
}
