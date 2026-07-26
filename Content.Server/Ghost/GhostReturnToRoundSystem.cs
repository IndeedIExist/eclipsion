using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._Crescent.GameRules.Components;
using Content.Shared.Database;
using Content.Shared.CCVar;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server.Ghost;

public sealed class GhostReturnToRoundSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mindSystem = default!; // WD EDIT

    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<GhostReturnToRoundRequest>(OnGhostReturnToRoundRequest);
    }

    private void OnGhostReturnToRoundRequest(GhostReturnToRoundRequest msg, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;

        if (uid == null)
            return;

        var connectedClient = args.SenderSession.Channel;
        var userId = args.SenderSession.UserId;

        TryGhostReturnToRound(uid.Value, connectedClient, userId, out var message, out var wrappedMessage);

        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Server,
            message,
            wrappedMessage,
            default,
            false,
            connectedClient,
            Color.Red);
    }

    private void TryGhostReturnToRound(EntityUid uid, INetChannel connectedClient, NetUserId userId, out string message, out string wrappedMessage)
    {
        var maxPlayers = _cfg.GetCVar(CCVars.GhostRespawnMaxPlayers);
        if (_playerManager.PlayerCount >= maxPlayers)
        {
            message = Loc.GetString("ghost-respawn-max-players", ("players", maxPlayers));
            wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            return;
        }

        if (!TryComp<GhostComponent>(uid, out var ghostComp))
        {
            message = Loc.GetString("ghost-respawn-error", ("players", maxPlayers));
            wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            return;
        }

        var deathTime = ghostComp.TimeOfDeath;
        // WD EDIT START
        if (_mindSystem.TryGetMind(uid, out _, out var mind) && mind.TimeOfDeath.HasValue)
            deathTime = mind.TimeOfDeath.Value;

        var timeUntilRespawn = GetGamemodeRespawnTime();
        var timePast = _gameTiming.CurTime - deathTime;
        // WD EDIT END
        if (timePast >= timeUntilRespawn)
        {
            _playerManager.TryGetSessionById(userId, out var targetPlayer);

            if (targetPlayer != null)
                _ticker.Respawn(targetPlayer);

            _adminLogger.Add(LogType.Mind, LogImpact.Medium, $"{Loc.GetString("ghost-respawn-log-return-to-lobby", ("userName", connectedClient.UserName))}");

            message = Loc.GetString("ghost-respawn-window-rules-footer");
            wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));

            return;
        }

        // WD EDIT START
        var timeLeft = timeUntilRespawn - timePast;
        message = timeLeft.Minutes > 0
            ? Loc.GetString("ghost-respawn-minutes-left", ("time", timeLeft.Minutes))
            : Loc.GetString("ghost-respawn-seconds-left", ("time", timeLeft.Seconds));
        // WD EDIT END

        wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
    }

    /// <summary>
    /// Returns the respawn time for the current active gamemode.
    /// Checks for a <see cref="GamemodeRespawnTimeComponent"/> on any active game rule;
    /// falls back to the ghost.respawn_time CVar if none is found.
    /// </summary>
    private TimeSpan GetGamemodeRespawnTime()
    {
        var query = EntityQueryEnumerator<GamemodeRespawnTimeComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var respawnComp, out var rule))
        {
            if (!_ticker.IsGameRuleActive(uid, rule))
                continue;

            return TimeSpan.FromMinutes(respawnComp.RespawnTimeMinutes);
        }

        return TimeSpan.FromMinutes(_cfg.GetCVar(CCVars.GhostRespawnTime));
    }
}
