using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class MassCloakConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool MassCloakEnabled;
    public float MassCloakRange;
    public float MassCloakMinRange;
    public float MassCloakMaxRange;
}

[Serializable, NetSerializable]
public enum MassCloakConsoleUiKey : byte
{
    Key,
}
