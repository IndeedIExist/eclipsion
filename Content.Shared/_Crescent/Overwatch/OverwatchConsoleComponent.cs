using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Компонент консоли Overwatch для отслеживания членов фракции.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchConsoleComponent : Component
{
    /// <summary>
    /// Фракция, которую отслеживает эта консоль.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Faction = string.Empty;

    /// <summary>
    /// Текущий фильтр по статусу.
    /// </summary>
    [DataField, AutoNetworkedField]
    public OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Текущий фильтр по отряду.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? SquadFilter;

    /// <summary>
    /// Текущий поисковый запрос.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SearchQuery = string.Empty;
}
