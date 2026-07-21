using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Data about a faction member, for display in the Overwatch console.
/// </summary>
[Serializable, NetSerializable]
public sealed record OverwatchMemberData(
    NetEntity Member,
    string Name,
    string JobTitle,
    OverwatchMemberStatus Status,
    bool HasCamera,
    int? SquadId = null,
    string SquadName = "",
    Vector2? Coordinates = null
);

/// <summary>
/// A faction member's status, for display in the Overwatch console.
/// </summary>
[Serializable, NetSerializable]
public enum OverwatchMemberStatus : byte
{
    Alive,
    Dead,
    SSD
}
