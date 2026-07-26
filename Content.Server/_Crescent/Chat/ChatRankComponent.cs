namespace Content.Server.Crescent.Chat;

/// <summary>
/// Stores "rank" of the user for certain contexts.
/// Mostly given by role.
/// </summary>

[RegisterComponent]
public sealed partial class ChatRankComponent : Component
{
    /// <summary>The floor rank a body carries before any role grants one. Treated as "no rank" by rank checks.</summary>
    public const string DefaultRank = "crescent-rank-private";

    [DataField]
    public string Rank = DefaultRank;
}
