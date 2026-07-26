using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Sits on a player who is looking through another player's camera via the Overwatch console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchWatchingComponent : Component
{
    /// <summary>
    /// The entity the player is currently watching.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;

    /// <summary>
    /// The Overwatch console entity driving this watch.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    /// <summary>
    /// The camera entity the watching happens through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Camera;
}
