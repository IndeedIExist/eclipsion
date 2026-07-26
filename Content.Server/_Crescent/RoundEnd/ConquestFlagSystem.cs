using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._Crescent.HullrotFaction;
using Content.Shared._Crescent.RoundEnd;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// Runs the ground game for <see cref="ConquestFlagComponent"/>: you walk up to an enemy banner, click it, and stand
/// there for <c>CaptureTime</c> seconds while a do-after runs. Move or take a hit and the attempt dies with nothing
/// saved — same feel as the Unionfall control point, not the planetfall capture ring. Flipping a banner only changes
/// who holds it; whether that costs a station its seat of power is decided by <see cref="FactionConquestRuleSystem"/>,
/// which reads the banners each check. This system draws nothing on the client; feedback is the do-after bar, a popup
/// on flip and examine text.
/// </summary>
public sealed class ConquestFlagSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConquestFlagComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ConquestFlagComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ConquestFlagComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ConquestFlagComponent, ConquestFlagCaptureDoAfterEvent>(OnCaptureDoAfter);
        SubscribeLocalEvent<ConquestFlagComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(EntityUid uid, ConquestFlagComponent flag, MapInitEvent args)
    {
        flag.CaptureTime = MathF.Max(1f, flag.CaptureTime);
        flag.CaptureRange = MathF.Max(0.25f, flag.CaptureRange);
        flag.UnlockTime = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(0f, flag.GracePeriod));

        ResolveHome(uid, flag);
    }

    /// <summary>Grabbing the banner with a free hand works the same as pressing E on it.</summary>
    private void OnInteractHand(EntityUid uid, ConquestFlagComponent flag, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartCapture(uid, flag, args.User);
    }

    private void OnActivate(EntityUid uid, ConquestFlagComponent flag, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartCapture(uid, flag, args.User);
    }

    /// <summary>
    /// Vets the clicker and opens the capture do-after. Returns true once the interaction has been answered one way
    /// or another — a refusal still counts, so nothing else tries to handle the same click.
    /// </summary>
    private bool TryStartCapture(EntityUid uid, ConquestFlagComponent flag, EntityUid user)
    {
        // The banner may have been planted before its grid received FactionStation, so settle the home faction the
        // first time anyone actually touches it.
        ResolveHome(uid, flag);

        if (_timing.CurTime < flag.UnlockTime)
        {
            var left = (int) MathF.Ceiling((float) (flag.UnlockTime - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("conquest-flag-grace", ("seconds", left)), uid, user);
            return true;
        }

        if (!TryComp<HullrotFactionComponent>(user, out var faction) || string.IsNullOrWhiteSpace(faction.Faction))
        {
            _popup.PopupEntity(Loc.GetString("conquest-flag-no-faction"), uid, user);
            return true;
        }

        if (string.Equals(faction.Faction, flag.OwnerFaction, StringComparison.Ordinal))
        {
            _popup.PopupEntity(Loc.GetString("conquest-flag-already-yours"), uid, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(flag.CaptureTime),
            new ConquestFlagCaptureDoAfterEvent(), uid, uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = flag.CaptureRange,
            NeedHand = false,
            // A second click must not throw away a capture already in progress — it is simply ignored.
            BlockDuplicate = true,
            CancelDuplicate = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(Loc.GetString("conquest-flag-already-working"), uid, user);
            return true;
        }

        flag.ContestingFaction = faction.Faction;
        flag.ContestUntil = _timing.CurTime + TimeSpan.FromSeconds(flag.CaptureTime);

        _popup.PopupEntity(Loc.GetString("conquest-flag-capture-begin-self"), uid, user);
        // Everyone who can see the banner gets the warning, so defenders have something to answer.
        _popup.PopupEntity(Loc.GetString("conquest-flag-capture-begin-others", ("faction", faction.Faction)),
            uid, Filter.PvsExcept(user), true, PopupType.MediumCaution);

        return true;
    }

    private void OnCaptureDoAfter(EntityUid uid, ConquestFlagComponent flag, ConquestFlagCaptureDoAfterEvent args)
    {
        flag.ContestingFaction = null;
        flag.ContestUntil = TimeSpan.Zero;

        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<HullrotFactionComponent>(args.User, out var faction) || string.IsNullOrWhiteSpace(faction.Faction))
            return;

        // Someone else may have flipped it while this capture ran.
        if (string.Equals(faction.Faction, flag.OwnerFaction, StringComparison.Ordinal))
            return;

        flag.OwnerFaction = faction.Faction;
        args.Handled = true;

        var msg = string.Equals(faction.Faction, flag.HomeFaction, StringComparison.Ordinal)
            ? Loc.GetString("conquest-flag-reclaimed", ("faction", faction.Faction))
            : Loc.GetString("conquest-flag-captured", ("faction", faction.Faction));
        // Local to whoever can see the banner — the sector-wide beat is the conquest rule's capture announcement,
        // fired only once the station's LAST banner falls, so many-banner stations do not spam everyone on each flip.
        _popup.PopupEntity(msg, uid, Filter.Pvs(uid), true, PopupType.LargeCaution);
    }

    private void OnExamine(EntityUid uid, ConquestFlagComponent flag, ExaminedEvent args)
    {
        ResolveHome(uid, flag);

        var home = string.IsNullOrWhiteSpace(flag.HomeFaction) ? "—" : flag.HomeFaction;
        args.PushMarkup(Loc.GetString("conquest-flag-examine-home", ("faction", home)));

        if (!string.IsNullOrWhiteSpace(flag.OwnerFaction))
        {
            args.PushMarkup(Loc.GetString(
                string.Equals(flag.OwnerFaction, flag.HomeFaction, StringComparison.Ordinal)
                    ? "conquest-flag-examine-held-home"
                    : "conquest-flag-examine-held-enemy",
                ("faction", flag.OwnerFaction)));
        }

        if (_timing.CurTime < flag.UnlockTime)
        {
            var left = (int) MathF.Ceiling((float) (flag.UnlockTime - _timing.CurTime).TotalSeconds);
            args.PushMarkup(Loc.GetString("conquest-flag-examine-grace", ("seconds", left)));
        }
        else if (flag.ContestingFaction != null && _timing.CurTime < flag.ContestUntil)
        {
            args.PushMarkup(Loc.GetString("conquest-flag-examine-capturing", ("faction", flag.ContestingFaction)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("conquest-flag-examine-hint", ("seconds", (int) flag.CaptureTime)));
        }
    }

    /// <summary>Fills in a blank home faction from the station grid the banner is planted on, once that grid has one.</summary>
    private void ResolveHome(EntityUid uid, ConquestFlagComponent flag)
    {
        if (string.IsNullOrWhiteSpace(flag.HomeFaction))
        {
            if (Transform(uid).GridUid is not { } grid ||
                !TryComp<FactionStationComponent>(grid, out var station) ||
                string.IsNullOrWhiteSpace(station.Faction))
                return;

            flag.HomeFaction = station.Faction;
        }

        flag.OwnerFaction ??= flag.HomeFaction;
    }
}
