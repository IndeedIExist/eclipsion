using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Request to refresh the faction member list.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchRefreshMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// UI state carrying the faction member data.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchUpdateState : BoundUserInterfaceState
{
    /// <summary>
    /// The faction members' data.
    /// </summary>
    public readonly List<OverwatchMemberData> Members;

    /// <summary>
    /// Available squads (ID -> name).
    /// </summary>
    public readonly Dictionary<int, string> AvailableSquads;

    /// <summary>
    /// Current status filter.
    /// </summary>
    public readonly OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Current squad filter.
    /// </summary>
    public readonly int? SquadFilter;

    /// <summary>
    /// Current search query.
    /// </summary>
    public readonly string SearchQuery;

    /// <summary>
    /// Faction colour for the UI.
    /// </summary>
    public readonly Color FactionColor;

    public OverwatchUpdateState(
        List<OverwatchMemberData> members,
        Dictionary<int, string>? availableSquads = null,
        OverwatchMemberStatus? statusFilter = null,
        int? squadFilter = null,
        string searchQuery = "",
        Color factionColor = default)
    {
        Members = members;
        AvailableSquads = availableSquads ?? new Dictionary<int, string>();
        StatusFilter = statusFilter;
        SquadFilter = squadFilter;
        SearchQuery = searchQuery;
        FactionColor = factionColor;
    }
}

/// <summary>
/// Request to view the target's camera.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchViewCameraMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The target entity to watch.
    /// </summary>
    public readonly NetEntity Target;

    public OverwatchViewCameraMessage(NetEntity target)
    {
        Target = target;
    }
}

/// <summary>
/// Request to stop watching.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchStopWatchingMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Request to set the status filter.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetStatusFilterMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The status to filter by.
    /// </summary>
    public readonly OverwatchMemberStatus? Status;

    public OverwatchSetStatusFilterMessage(OverwatchMemberStatus? status)
    {
        Status = status;
    }
}

/// <summary>
/// Request to set the squad filter.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetSquadFilterMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The squad ID to filter by.
    /// </summary>
    public readonly int? SquadId;

    public OverwatchSetSquadFilterMessage(int? squadId)
    {
        SquadId = squadId;
    }
}

/// <summary>
/// Request to set the search query.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSetSearchMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The search query string.
    /// </summary>
    public readonly string SearchQuery;

    public OverwatchSetSearchMessage(string searchQuery)
    {
        SearchQuery = searchQuery;
    }
}

/// <summary>
/// Request to create a new squad.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchCreateSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The new squad's name.
    /// </summary>
    public readonly string SquadName;

    public OverwatchCreateSquadMessage(string squadName)
    {
        SquadName = squadName;
    }
}

/// <summary>
/// Request to delete a squad.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchDeleteSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The ID of the squad to delete.
    /// </summary>
    public readonly int SquadId;

    public OverwatchDeleteSquadMessage(int squadId)
    {
        SquadId = squadId;
    }
}

/// <summary>
/// Request to assign a player to a squad.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchAssignSquadMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The player entity.
    /// </summary>
    public readonly NetEntity Player;

    /// <summary>
    /// The ID of the squad to assign them to.
    /// </summary>
    public readonly int SquadId;

    public OverwatchAssignSquadMessage(NetEntity player, int squadId)
    {
        Player = player;
        SquadId = squadId;
    }
}

/// <summary>
/// Request to remove a player from their squad.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchRemoveSquadMemberMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The player entity.
    /// </summary>
    public readonly NetEntity Player;

    public OverwatchRemoveSquadMemberMessage(NetEntity player)
    {
        Player = player;
    }
}

/// <summary>
/// Request to send an announcement.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchSendMessageAnnouncement : BoundUserInterfaceMessage
{
    /// <summary>
    /// The announcement text.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// The target squad's ID, or null for the whole faction.
    /// </summary>
    public readonly int? TargetSquadId;

    public OverwatchSendMessageAnnouncement(string message, int? targetSquadId)
    {
        Message = message;
        TargetSquadId = targetSquadId;
    }
}

/// <summary>
/// Event for displaying an Overwatch announcement.
/// </summary>
[Serializable, NetSerializable]
public sealed class OverwatchAnnouncementEvent : EntityEventArgs
{
    /// <summary>
    /// The announcement text.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// The recipient's name (faction or squad).
    /// </summary>
    public readonly string TargetName;

    /// <summary>
    /// The faction's Overwatch name.
    /// </summary>
    public readonly string OverwatchTitle;

    /// <summary>
    /// The announcement text colour.
    /// </summary>
    public readonly Color Color;

    public OverwatchAnnouncementEvent(string message, string targetName, string overwatchTitle, Color color)
    {
        Message = message;
        TargetName = targetName;
        OverwatchTitle = overwatchTitle;
        Color = color;
    }
}
