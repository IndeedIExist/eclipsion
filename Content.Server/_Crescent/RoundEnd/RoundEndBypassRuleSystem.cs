using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// Bookkeeping for <see cref="RoundEndBypassRuleComponent"/>. The actual suppression lives in
/// <see cref="GameTicking.GameTicker.IsRoundEndBypassed"/> and its callers — this only tells admins the round is
/// now open-ended, since a round that refuses to end is confusing otherwise.
/// </summary>
public sealed class RoundEndBypassRuleSystem : GameRuleSystem<RoundEndBypassRuleComponent>
{
    [Dependency] private readonly IChatManager _chat = default!;

    protected override void Started(EntityUid uid, RoundEndBypassRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        Log.Info("Round end bypass active: this round will not end on its own.");
        _chat.SendAdminAnnouncement(Loc.GetString("round-end-bypass-active"));
    }
}
