using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Overwatch console component, for tracking faction members.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchConsoleComponent : Component
{
    /// <summary>
    /// The faction this console tracks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Faction = string.Empty;

    /// <summary>
    /// Current status filter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Current squad filter.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? SquadFilter;

    /// <summary>
    /// Current search query.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SearchQuery = string.Empty;
}
