using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public enum SlotSymbol : byte
{
    Cherry,
    Lemon,
    Bell,
    Star,
    Diamond,
    Seven,
}

[Serializable, NetSerializable]
public enum RouletteBetKind : byte
{
    Red,
    Black,
    Odd,
    Even,
    Low,
    High,
    Dozen1,
    Dozen2,
    Dozen3,
    Straight,
}

[Serializable, NetSerializable]
public enum BlackjackAction : byte
{
    Deal,
    Hit,
    Stand,
}

[Serializable, NetSerializable]
public struct GamblingCard
{
    /// <summary>1 = Ace, 11 = Jack, 12 = Queen, 13 = King.</summary>
    public byte Rank;

    /// <summary>0 = Spades, 1 = Hearts, 2 = Diamonds, 3 = Clubs.</summary>
    public byte Suit;

    public GamblingCard(byte rank, byte suit)
    {
        Rank = rank;
        Suit = suit;
    }
}

[Serializable, NetSerializable]
public sealed class GamblingUiState : BoundUserInterfaceState
{
    public long Balance;

    /// <summary>Red toast. Set when the last action was rejected.</summary>
    public string? Error;

    /// <summary>Result toast for a resolved bet. Green when <see cref="ToastGood"/>.</summary>
    public string? Toast;
    public bool ToastGood;

    /// <summary>Null until the player has spun at least once this shift.</summary>
    public SlotSymbol[]? Reels;

    /// <summary>Wheel pocket index of the last spin, or -1 if never spun. See <see cref="GamblingTables"/>.</summary>
    public int LastPocket;

    public bool HandActive;
    public List<GamblingCard> PlayerCards;
    public List<GamblingCard> DealerCards;

    /// <summary>While true the client must not render or total the dealer's second card.</summary>
    public bool DealerHoleHidden;
    public int HandBet;

    public GamblingUiState(
        long balance,
        string? error,
        string? toast,
        bool toastGood,
        SlotSymbol[]? reels,
        int lastPocket,
        bool handActive,
        List<GamblingCard> playerCards,
        List<GamblingCard> dealerCards,
        bool dealerHoleHidden,
        int handBet)
    {
        Balance = balance;
        Error = error;
        Toast = toast;
        ToastGood = toastGood;
        Reels = reels;
        LastPocket = lastPocket;
        HandActive = handActive;
        PlayerCards = playerCards;
        DealerCards = dealerCards;
        DealerHoleHidden = dealerHoleHidden;
        HandBet = handBet;
    }
}

[Serializable, NetSerializable]
public sealed class GamblingSpinSlotsMessage : CartridgeMessageEvent
{
    public int Bet;

    public GamblingSpinSlotsMessage(int bet)
    {
        Bet = bet;
    }
}

[Serializable, NetSerializable]
public sealed class GamblingRouletteBetMessage : CartridgeMessageEvent
{
    public RouletteBetKind Kind;

    /// <summary>Only meaningful for <see cref="RouletteBetKind.Straight"/>; 1-36.</summary>
    public int Number;
    public int Bet;

    public GamblingRouletteBetMessage(RouletteBetKind kind, int number, int bet)
    {
        Kind = kind;
        Number = number;
        Bet = bet;
    }
}

[Serializable, NetSerializable]
public sealed class GamblingBlackjackMessage : CartridgeMessageEvent
{
    public BlackjackAction Action;

    /// <summary>Only read for <see cref="BlackjackAction.Deal"/>.</summary>
    public int Bet;

    public GamblingBlackjackMessage(BlackjackAction action, int bet)
    {
        Action = action;
        Bet = bet;
    }
}
