using Robust.Shared.GameObjects;

namespace Content.Shared._Crescent.RoundEnd;

/// <summary>
/// Raised once when a faction's station is written off as lost (dark past the blackout grace period, or
/// outright destroyed). Recovering power does not un-raise it — a station knocked out twice reports twice.
/// </summary>
[ByRefEvent]
public readonly record struct FactionStationFellEvent(EntityUid Station, string Faction, string StationName);

/// <summary>
/// Raised when the war is settled and the winners are locked in, just before the round ends.
/// </summary>
[ByRefEvent]
public readonly record struct FactionWarDecidedEvent(List<string> Winners);
