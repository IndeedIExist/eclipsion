using Content.Shared._Crescent.CartridgeLoader.Cartridges;

namespace Content.Server._Crescent.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class GamblingCartridgeComponent : Component
{
    /// <summary>
    /// Earliest time the next money-committing action (spin or deal) is allowed. Hitting and
    /// standing are deliberately exempt so blackjack does not feel sluggish.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAction = TimeSpan.Zero;

    /// <summary>Reels of the last spin, or null if this cartridge has never been spun.</summary>
    [ViewVariables]
    public SlotSymbol[]? LastReels;

    /// <summary>Wheel pocket of the last roulette spin, or -1 if never spun.</summary>
    [ViewVariables]
    public int LastPocket = -1;

    /// <summary>
    /// The mob whose blackjack hand is on the table. Only this mob may hit or stand.
    /// </summary>
    [ViewVariables]
    public EntityUid? HandOwner;

    [ViewVariables]
    public bool HandActive;

    [ViewVariables]
    public int HandBet;

    [ViewVariables]
    public List<GamblingCard> PlayerCards = new();

    [ViewVariables]
    public List<GamblingCard> DealerCards = new();

    /// <summary>False once the hand is resolved and the hole card has been turned up.</summary>
    [ViewVariables]
    public bool DealerHoleHidden;
}
