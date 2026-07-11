using Robust.Shared.Audio;

namespace Content.Shared._Crescent.Taxation;

/// <summary>
/// A per-faction treasury console. Displays and dispenses the tax revenue accumulated in the
/// owning station's <c>StationTradeMarketComponent</c>. Access is gated by the console's
/// <c>AccessReader</c> (faction funds access): anyone without access cannot open the UI at all
/// and instead triggers a (rate-limited) intrusion alarm.
/// </summary>
[RegisterComponent]
public sealed partial class FactionTreasuryConsoleComponent : Component
{
    /// <summary>
    /// Faction key this vault belongs to (e.g. "DSM", "NCWL", "SHI"). The intrusion alert is sent
    /// only to this faction's members via the overwatch system. Empty disables the broadcast.
    /// </summary>
    [DataField]
    public string Faction = string.Empty;

    /// <summary>
    /// Sound played when someone without access tries to open the console.
    /// </summary>
    [DataField]
    public SoundSpecifier AlarmSound =
        new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    /// <summary>
    /// Minimum time between two intrusion-alarm plays, so repeated access attempts can't
    /// spam the local sound. Prevents the "constantly blaring" behaviour.
    /// </summary>
    [DataField]
    public TimeSpan AlarmCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Minimum time between two sector-wide intrusion announcements. Much longer than the local
    /// alarm cooldown so a would-be thief can't spam a global broadcast.
    /// </summary>
    [DataField]
    public TimeSpan AnnounceCooldown = TimeSpan.FromSeconds(60);

    // --- Runtime state (not authored in YAML) -----------------------------

    /// <summary>Server time the intrusion alarm last played, for cooldown tracking.</summary>
    [ViewVariables]
    public TimeSpan? LastAlarm;

    /// <summary>Server time the sector-wide intrusion announcement last fired.</summary>
    [ViewVariables]
    public TimeSpan? LastAnnounce;
}
