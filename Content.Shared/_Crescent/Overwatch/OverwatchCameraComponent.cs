using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Компонент для сущности, на которую смотрят через консоль Overwatch.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchCameraComponent : Component
{
    /// <summary>
    /// Игроки, которые сейчас смотрят через камеру этой сущности.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Watching = new();
}
