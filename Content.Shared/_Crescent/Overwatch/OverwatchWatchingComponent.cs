using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Компонент для игрока, который смотрит через камеру другого игрока через консоль Overwatch.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RatOverwatchWatchingComponent : Component
{
    /// <summary>
    /// Сущность, за которой сейчас наблюдает игрок.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;

    /// <summary>
    /// Сущность консоли Overwatch, которая управляет этим наблюдением.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    /// <summary>
    /// Сущность камеры, через которую осуществляется наблюдение.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Camera;
}
