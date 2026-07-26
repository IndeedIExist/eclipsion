using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Poker;

[Serializable, NetSerializable]
public enum CardSuit : byte
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

[Serializable, NetSerializable]
public enum CardRank : byte
{
    Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
    Jack, Queen, King, Ace
}

[Serializable, NetSerializable]
public enum PokerRoundPhase : byte
{
    Waiting,
    PreFlop,
    Flop,
    Turn,
    River,
    Showdown
}

[Serializable, NetSerializable]
public enum PokerPlayerStatus : byte
{
    Waiting,
    Active,
    Folded,
    AllIn,
    Winner
}

[Serializable, NetSerializable]
public enum HandRank : byte
{
    HighCard,
    OnePair,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush
}

[Serializable, NetSerializable]
public enum PokerUiKey : byte
{
    Key
}
