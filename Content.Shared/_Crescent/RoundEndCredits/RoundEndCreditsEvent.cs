using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.RoundEndCredits;

/// <summary>
/// Event sent from server to clients at round end containing player/character info for credits display.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundEndCreditsEvent : EntityEventArgs
{
    public List<CreditsPlayerInfo> Players { get; }

    public RoundEndCreditsEvent(List<CreditsPlayerInfo> players)
    {
        Players = players;
    }
}

[Serializable, NetSerializable]
public sealed class CreditsPlayerInfo
{
    public string PlayerName { get; }
    public string CharacterName { get; }
    public string Role { get; }
    public NetEntity? Entity { get; }

    public CreditsPlayerInfo(string playerName, string characterName, string role, NetEntity? entity)
    {
        PlayerName = playerName;
        CharacterName = characterName;
        Role = role;
        Entity = entity;
    }
}
