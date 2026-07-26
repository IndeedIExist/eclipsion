using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Sits on an entity that is being watched through the Overwatch console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchCameraComponent : Component
{
    /// <summary>
    /// The players currently looking through this entity's camera.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Watching = new();
}
