using Robust.Shared.GameObjects;

namespace Content.Shared._Crescent.RoundEnd;

/// <summary>
/// Raised on the round-end computer when a mission is successfully turned in, after its items are consumed but
/// before/around the announcement. This is the reward hook: the reward pass subscribes here to grant whatever the
/// faction earns. Nothing listens yet — rewards are handled later.
/// </summary>
[ByRefEvent]
public readonly record struct FactionMissionCompletedEvent(
    EntityUid Console,
    EntityUid Actor,
    string Faction,
    FactionMissionPrototype Mission);
