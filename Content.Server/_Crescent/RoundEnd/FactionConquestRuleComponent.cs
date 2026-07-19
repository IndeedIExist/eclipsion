namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// Configures the conquest win condition: a faction is eliminated once every station it owns has fallen, and the
/// round ends when only one alliance bloc is left standing.
/// </summary>
[RegisterComponent, Access(typeof(FactionConquestRuleSystem))]
public sealed partial class FactionConquestRuleComponent : Component
{
    /// <summary>
    /// The great powers. A surviving bloc containing exactly one of these is credited to that faction, however
    /// many minor allies stand with it. If both majors fall the minors do NOT have to finish each other off —
    /// every surviving faction takes the sector together.
    /// </summary>
    [DataField]
    public List<string> MajorFactions = new() { "DSM", "NCWL" };

    /// <summary>How long a station must sit without power before it counts as fallen.</summary>
    [DataField]
    public TimeSpan BlackoutToFall = TimeSpan.FromMinutes(10);

    /// <summary>How often the win condition is evaluated.</summary>
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    /// <summary>Delay between declaring a winner and restarting.</summary>
    [DataField]
    public TimeSpan RestartDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Grace window between the war looking settled and the round actually ending, so the factions still standing
    /// get one last chance to move — restore a station's power and the pending victory is called off.
    /// </summary>
    [DataField]
    public TimeSpan VictoryDelay = TimeSpan.FromMinutes(20);

    /// <summary>Faction id -> the message broadcast when it wins.</summary>
    [DataField]
    public Dictionary<string, string> VictoryAnnouncements = new();

    /// <summary>Broadcast when both great powers are gone and the surviving minors inherit the sector.</summary>
    [DataField]
    public string MinorVictoryAnnouncement = "faction-victory-minors";

    /// <summary>Broadcast when the round ends with nobody having won.</summary>
    [DataField]
    public string TimeoutAnnouncement = "faction-victory-timeout";

    /// <summary>When the next evaluation is due.</summary>
    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>Station grid -> when it went dark. Cleared as soon as power returns.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> DarkSince = new();

    /// <summary>Stations whose fall has already been broadcast, so it is announced once.</summary>
    [ViewVariables]
    public HashSet<EntityUid> AnnouncedFallen = new();

    /// <summary>Factions currently set to win, and when the countdown started. Cleared if the war reopens.</summary>
    [ViewVariables]
    public List<string>? PendingWinners;

    [ViewVariables]
    public TimeSpan PendingSince;

    /// <summary>Set once a winner has been declared so the round only ends once.</summary>
    [ViewVariables]
    public bool Decided;
}
