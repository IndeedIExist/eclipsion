namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// While this rule is running the round can never end on its own: every path that ends a round funnels through
/// <see cref="GameTicking.GameTicker.EndRound"/>, which drops the request while this is active, and the two rules
/// that restart the round on a timer (max round time, empty-server inactivity) skip their restart.
///
/// This is a mapping/dev tool. It rides along with the Mapping preset, and admins can flip it on mid-round with
/// `addgamerule RoundEndBypass`. Nothing here blocks `restartround` / `restartroundnow` — that is how you end a
/// bypassed round.
/// </summary>
[RegisterComponent]
public sealed partial class RoundEndBypassRuleComponent : Component
{
}
