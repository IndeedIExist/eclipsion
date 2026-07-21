using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Squad;

/// <summary>
/// Marks which squad an entity belongs to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SquadComponent : Component
{
    /// <summary>
    /// Squad ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SquadId;

    /// <summary>
    /// Squad name, for display
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SquadName = string.Empty;
}
