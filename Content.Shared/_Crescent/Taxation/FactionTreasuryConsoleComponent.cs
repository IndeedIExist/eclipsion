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

    /// <summary>
    /// Maximum fraction (0..1) of the treasury any single faction member may withdraw in one round
    /// across all of this faction's consoles. Prevents one person draining the vault; the treasury
    /// keeps enough for the faction to actually use. Defaults to 50%.
    /// </summary>
    [DataField]
    public float MaxWithdrawFraction = 0.50f;

    // --- Robbery / breach -------------------------------------------------

    /// <summary>
    /// Fraction (0..1) of the current treasury spilled out as physical cash on each loot tick once a
    /// breach matures. Kept low so the vault bleeds out slowly over the course of the heist rather
    /// than emptying in seconds; combined with <see cref="LootMinimum"/> this still lets thieves walk
    /// away with a real chunk given enough time.
    /// </summary>
    [DataField]
    public float LootFraction = 0.05f;

    /// <summary>Minimum credits spilled per loot tick, so even small vaults visibly drain.</summary>
    [DataField]
    public int LootMinimum = 100;

    /// <summary>Upper bound on how much a single loot tick can spill, so huge vaults still drain gradually.</summary>
    [DataField]
    public int LootMaximum = 2000;

    /// <summary>
    /// Grace period after an unauthorized breach begins before the vault actually starts leaking
    /// cash, giving the faction a window to respond before the robbery pays out.
    /// </summary>
    [DataField]
    public TimeSpan LootDelay = TimeSpan.FromSeconds(60);

    /// <summary>Time between two loot ticks while a breach is active and past its grace period.</summary>
    [DataField]
    public TimeSpan LootInterval = TimeSpan.FromSeconds(15);

    // --- Runtime state (not authored in YAML) -----------------------------

    /// <summary>Server time the intrusion alarm last played, for cooldown tracking.</summary>
    [ViewVariables]
    public TimeSpan? LastAlarm;

    /// <summary>Server time the sector-wide intrusion announcement last fired.</summary>
    [ViewVariables]
    public TimeSpan? LastAnnounce;

    /// <summary>
    /// Server time the current unauthorized breach began, or null when the vault is secure. While set,
    /// the vault is being robbed and (after <see cref="LootDelay"/>) leaks cash until an authorized
    /// member re-secures it by operating the console.
    /// </summary>
    [ViewVariables]
    public TimeSpan? BreachStart;

    /// <summary>Server time the active breach last spilled cash, for loot-tick pacing.</summary>
    [ViewVariables]
    public TimeSpan? LastLoot;
}
