namespace Content.Shared._Crescent.RoundEnd;

/// <summary>
/// A faction banner planted on a station by hand. Anyone whose <c>HullrotFaction</c> differs from the current holder
/// flips it to their side by walking up to it, clicking it, and standing still for <see cref="CaptureTime"/> seconds;
/// the station the banner sits on is written off by
/// <see cref="Content.Server._Crescent.RoundEnd.FactionConquestRuleSystem"/> once every banner aboard has been in
/// enemy hands for the conquest rule's hold window.
///
/// Unlike the planetfall <c>CaptureFlag</c> this is not an area you stand in — nothing happens by proximity alone, and
/// there is no capture ring, no contest tug-of-war and no progress bleed. It is deliberately the same interaction as
/// the Unionfall control point: one click, one do-after, and getting shot or walking off cancels it outright. The
/// banner is pure ground state that the conquest win condition reads; the 10-minute hold clock and the "all banners
/// taken" check live on the conquest rule, not here.
/// </summary>
[RegisterComponent]
public sealed partial class ConquestFlagComponent : Component
{
    /// <summary>
    /// Faction this banner belongs to. Left blank on the prototype and resolved from the grid's
    /// <see cref="FactionStationComponent"/> on demand, so a mapper only has to plant it on the right station.
    /// </summary>
    [DataField]
    public string? HomeFaction;

    /// <summary>Faction that currently holds the banner. Starts equal to <see cref="HomeFaction"/>.</summary>
    [DataField]
    public string? OwnerFaction;

    /// <summary>
    /// Seconds the capturer must stand at the banner after clicking it. Taking damage or moving cancels the attempt
    /// and nothing is kept — there is no partial progress.
    /// </summary>
    [DataField]
    public float CaptureTime = 20f;

    /// <summary>How far the capturer may be from the banner before the attempt breaks, in tiles.</summary>
    [DataField]
    public float CaptureRange = 2.0f;

    /// <summary>Seconds after map init before the banner can be captured at all. 0 = capturable immediately.</summary>
    [DataField]
    public float GracePeriod;

    /// <summary>
    /// Absolute time the grace period ends, baked from <see cref="GracePeriod"/> at map init. VV this to open or
    /// extend a banner mid-round.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan UnlockTime;

    /// <summary>Faction of the last mob to start a capture, for examine feedback. Transient, not saved.</summary>
    [ViewVariables]
    public string? ContestingFaction;

    /// <summary>
    /// When the in-progress capture would finish. Examine only reports a contest before this time, so a capturer who
    /// is gibbed mid-do-after (and never raises the finish event) cannot leave the banner looking contested forever.
    /// </summary>
    [ViewVariables]
    public TimeSpan ContestUntil;
}
