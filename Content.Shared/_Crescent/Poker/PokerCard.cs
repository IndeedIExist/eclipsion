using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Poker;

[Serializable, NetSerializable]
public sealed class PokerCard
{
    public CardSuit Suit { get; set; }
    public CardRank Rank { get; set; }

    public PokerCard(CardSuit suit, CardRank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public string GetSpriteName(string deckStyle = "nanotrasen")
    {
        var rankStr = Rank switch
        {
            CardRank.Ace => "Ace",
            CardRank.King => "King",
            CardRank.Queen => "Queen",
            CardRank.Jack => "Jack",
            CardRank.Ten => "10",
            _ => ((int)Rank).ToString()
        };

        var suitStr = Suit switch
        {
            CardSuit.Hearts => "Hearts",
            CardSuit.Diamonds => "Diamonds",
            CardSuit.Clubs => "Clubs",
            CardSuit.Spades => "Spades",
            _ => "Spades"
        };

        return $"sc_{rankStr}_of_{suitStr}_{deckStyle}";
    }

    public override string ToString() => $"{Rank} of {Suit}";
}
