using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Factions;

[Serializable, NetSerializable]
public sealed class RatFactionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsWhitelisted { get; set; }
}
