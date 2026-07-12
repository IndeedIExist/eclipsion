using Content.Shared._Crescent.RoundEndCredits;
using Content.Shared.GameTicking;

namespace Content.Server._Crescent.RoundEndCredits;

public sealed class RoundEndCreditsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        var players = new List<CreditsPlayerInfo>();

        foreach (var info in ev.AllPlayersEndInfo)
        {
            if (info.Observer)
                continue;

            var playerName = info.PlayerOOCName;
            var charName = info.PlayerICName ?? "Unknown";
            var role = info.Role ?? "Unknown";
            players.Add(new CreditsPlayerInfo(playerName, charName, role, info.PlayerNetEntity));
        }

        RaiseNetworkEvent(new RoundEndCreditsEvent(players));
    }
}
