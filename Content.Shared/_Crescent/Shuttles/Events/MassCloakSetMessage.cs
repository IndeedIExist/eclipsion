using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Shuttles.Events;

[Serializable, NetSerializable]
public sealed class MassCloakSetMessage : BoundUserInterfaceMessage
{
    public bool Enabled;
    public float Range;
}
