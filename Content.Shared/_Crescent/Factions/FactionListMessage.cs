using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Factions;

[Serializable, NetSerializable]
public sealed class FactionListMessage : EntityEventArgs
{
    public List<RatFactionInfo> Factions { get; set; } = new();
}

[Serializable, NetSerializable]
public sealed class RequestFactionListMessage : EntityEventArgs
{
}
