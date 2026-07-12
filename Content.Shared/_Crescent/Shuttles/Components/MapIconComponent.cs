using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Shuttles.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapIconComponent : Component
{
    [DataField("iconType"), AutoNetworkedField]
    public MapIconType Type = MapIconType.Shuttle;
}

[Serializable, NetSerializable]
public enum MapIconType : byte
{
    Shuttle,
    Station
}