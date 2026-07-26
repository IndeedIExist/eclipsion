using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.RoundEnd;

/// <summary>
/// A faction-owned "round-end" mission computer. Members feed a mission's required items into it by clicking the
/// computer with the item in hand — the item is consumed immediately and permanently, advancing that mission's
/// progress. Once every requirement is met an authorized member turns the mission in: a reward is granted (see
/// <see cref="FactionMissionCompletedEvent"/> — a placeholder for now). Each mission is one-and-done per round,
/// and finishing the console's whole set broadcasts <see cref="FinaleAnnouncement"/> to everyone.
/// </summary>
[RegisterComponent]
public sealed partial class FactionRoundEndComputerComponent : Component
{
    /// <summary>Display name of the owning faction (flavour / logs only). The AccessReader does the real gating.</summary>
    [DataField]
    public string Faction = string.Empty;

    /// <summary>The missions this computer offers, in display order.</summary>
    [DataField(required: true)]
    public List<ProtoId<FactionMissionPrototype>> Missions = new();

    /// <summary>Missions already turned in this round; each is one-and-done.</summary>
    [ViewVariables]
    public HashSet<string> Completed = new();

    /// <summary>
    /// How much has been delivered toward each mission, index-aligned to that mission's requirement entries.
    /// Persisted here (not as held items) so consumed materiel is gone for good.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, List<int>> Delivered = new();

    /// <summary>Who the sector-wide announcement is signed by when a mission lacks its own sender.</summary>
    [DataField]
    public LocId AnnouncementSender = "faction-roundend-announcer";

    /// <summary>
    /// The faction's payoff message, broadcast to everyone in bold orange (the server/admin chat style) once
    /// *every* mission on this console is complete — not per mission. Consoles carrying several directives
    /// (SHI, TFSC) therefore only fire it after the whole set is done.
    /// </summary>
    [DataField]
    public LocId? FinaleAnnouncement;
}

[Serializable, NetSerializable]
public enum FactionRoundEndUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FactionRoundEndConsoleState : BoundUserInterfaceState
{
    public readonly string FactionName;
    public readonly List<FactionMissionView> Missions;

    public FactionRoundEndConsoleState(string factionName, List<FactionMissionView> missions)
    {
        FactionName = factionName;
        Missions = missions;
    }
}

/// <summary>A mission and its live delivery progress, as shown to the client.</summary>
[Serializable, NetSerializable]
public sealed class FactionMissionView
{
    public string MissionId = default!;
    public string Name = default!;
    public string Description = string.Empty;

    /// <summary>Already turned in this round.</summary>
    public bool Completed;

    /// <summary>All requirements are currently met — the turn-in button is live.</summary>
    public bool Ready;

    public List<FactionMissionItemView> Items = new();
}

/// <summary>One required item line with how much has been delivered so far.</summary>
[Serializable, NetSerializable]
public sealed class FactionMissionItemView
{
    public string Name = default!;
    public int Required;
    public int Delivered;
}

/// <summary>Player pressed "turn in" on a mission whose requirements are met.</summary>
[Serializable, NetSerializable]
public sealed class FactionRoundEndSubmitMessage : BoundUserInterfaceMessage
{
    public readonly string MissionId;

    public FactionRoundEndSubmitMessage(string missionId)
    {
        MissionId = missionId;
    }
}
