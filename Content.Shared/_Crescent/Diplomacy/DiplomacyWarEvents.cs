using Robust.Shared.GameObjects;

namespace Content.Shared._Crescent.Diplomacy;

/// <summary>
/// Raised once when two factions cross into open war, from either relation table — the player-facing
/// diplomacy console or the admin-set IFF matrix. Only the transition raises it: a pair already at war
/// that is set to war again is not news, and re-raising would seize the same shares twice.
///
/// The pair is unordered. Handlers that care about direction must consider both.
/// </summary>
[ByRefEvent]
public readonly record struct FactionsWentToWarEvent(string FactionA, string FactionB);
