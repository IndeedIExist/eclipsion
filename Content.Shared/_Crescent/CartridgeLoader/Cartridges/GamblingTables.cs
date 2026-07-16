using System.Collections.Generic;

namespace Content.Shared._Crescent.CartridgeLoader.Cartridges;

/// <summary>
/// Odds, paytables and hand maths shared by the client (paytable display) and the
/// server (authoritative resolution). Every game here is tuned to roughly a 90% return
/// to player, so the house slowly drains wages rather than minting them.
/// </summary>
public static class GamblingTables
{
    #region Slots

    /// <summary>
    /// Symbol weights for a single reel. All three reels use this same distribution and are
    /// rolled independently. Weights sum to <see cref="SlotReelWeightTotal"/>.
    /// </summary>
    public static readonly (SlotSymbol Symbol, int Weight)[] SlotReel =
    {
        (SlotSymbol.Cherry, 30),
        (SlotSymbol.Lemon, 25),
        (SlotSymbol.Bell, 20),
        (SlotSymbol.Star, 14),
        (SlotSymbol.Diamond, 8),
        (SlotSymbol.Seven, 3),
    };

    public const int SlotReelWeightTotal = 100;

    /// <summary>Total payout multiplier of the bet when all three reels match.</summary>
    public static int SlotTripleMultiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 4,
        SlotSymbol.Lemon => 8,
        SlotSymbol.Bell => 12,
        SlotSymbol.Star => 25,
        SlotSymbol.Diamond => 50,
        SlotSymbol.Seven => 250,
        _ => 0,
    };

    /// <summary>
    /// Total payout multiplier when exactly two reels match. With three reels at most one
    /// symbol can appear exactly twice, so pair payouts can never stack.
    /// </summary>
    public static int SlotPairMultiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 1,
        SlotSymbol.Lemon => 1,
        SlotSymbol.Bell => 1,
        SlotSymbol.Star => 0,
        SlotSymbol.Diamond => 2,
        SlotSymbol.Seven => 5,
        _ => 0,
    };

    /// <summary>
    /// Returns the total payout multiplier for a spin: 0 for a loss, otherwise the multiplier
    /// applied to the original bet (already includes the stake, so 1 is a push).
    /// </summary>
    public static int SlotMultiplier(SlotSymbol a, SlotSymbol b, SlotSymbol c)
    {
        if (a == b && b == c)
            return SlotTripleMultiplier(a);

        if (a == b || a == c)
            return SlotPairMultiplier(a);

        if (b == c)
            return SlotPairMultiplier(b);

        return 0;
    }

    #endregion

    #region Roulette

    /// <summary>
    /// Pockets 0-3 are the green house pockets ("0", "00", "000", "0000"); pockets 4-39 are
    /// numbers 1-36. Paying true odds against 40 pockets gives every bet type an identical
    /// 36/40 = 90% return, so no bet is better than another and there is nothing to arbitrage.
    /// </summary>
    public const int RoulettePocketCount = 40;

    public const int RouletteGreenCount = 4;

    public static bool IsGreen(int pocket) => pocket is >= 0 and < RouletteGreenCount;

    /// <summary>Returns the 1-36 number of a pocket, or 0 for the green house pockets.</summary>
    public static int PocketNumber(int pocket) => IsGreen(pocket) ? 0 : pocket - RouletteGreenCount + 1;

    public static string PocketLabel(int pocket) => pocket switch
    {
        0 => "0",
        1 => "00",
        2 => "000",
        3 => "0000",
        _ => PocketNumber(pocket).ToString(),
    };

    private static readonly HashSet<int> RedNumbers = new()
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36,
    };

    /// <summary>Standard roulette colouring; 18 red and 18 black across 1-36.</summary>
    public static bool IsRed(int number) => RedNumbers.Contains(number);

    /// <summary>Total payout multiplier of the bet, including the stake.</summary>
    public static int RouletteMultiplier(RouletteBetKind kind) => kind switch
    {
        RouletteBetKind.Straight => 36,
        RouletteBetKind.Dozen1 or RouletteBetKind.Dozen2 or RouletteBetKind.Dozen3 => 3,
        _ => 2,
    };

    public static bool RouletteWins(RouletteBetKind kind, int number, int pocket)
    {
        // Green pockets are the house edge: every bet loses on them.
        if (IsGreen(pocket))
            return false;

        var n = PocketNumber(pocket);
        return kind switch
        {
            RouletteBetKind.Red => IsRed(n),
            RouletteBetKind.Black => !IsRed(n),
            RouletteBetKind.Odd => n % 2 == 1,
            RouletteBetKind.Even => n % 2 == 0,
            RouletteBetKind.Low => n <= 18,
            RouletteBetKind.High => n >= 19,
            RouletteBetKind.Dozen1 => n <= 12,
            RouletteBetKind.Dozen2 => n is >= 13 and <= 24,
            RouletteBetKind.Dozen3 => n >= 25,
            RouletteBetKind.Straight => n == number,
            _ => false,
        };
    }

    #endregion

    #region Blackjack

    /// <summary>
    /// House rules, chosen to land near 90%: the dealer hits soft 17, blackjack pays 3:2,
    /// there is no doubling or splitting, and the dealer takes pushes (a tie loses).
    /// The push rule is what carries most of the edge.
    /// </summary>
    public const int BlackjackDealerStand = 17;

    /// <summary>
    /// Totals a hand, demoting aces from 11 to 1 as needed. <c>soft</c> means an ace is still
    /// counted as 11, so the total can absorb one more hit without busting.
    /// </summary>
    public static (int Total, bool Soft) HandTotal(IReadOnlyList<GamblingCard> cards)
    {
        var total = 0;
        var aces = 0;

        foreach (var card in cards)
        {
            if (card.Rank == 1)
            {
                aces++;
                total += 11;
            }
            else
            {
                total += card.Rank >= 10 ? 10 : card.Rank;
            }
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        return (total, aces > 0);
    }

    public static bool IsBlackjack(IReadOnlyList<GamblingCard> cards)
        => cards.Count == 2 && HandTotal(cards).Total == 21;

    public static string RankLabel(byte rank) => rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => rank.ToString(),
    };

    public static string SuitLabel(byte suit) => suit switch
    {
        0 => "♠",
        1 => "♥",
        2 => "♦",
        _ => "♣",
    };

    public static bool SuitIsRed(byte suit) => suit is 1 or 2;

    public static string CardLabel(GamblingCard card) => $"{RankLabel(card.Rank)}{SuitLabel(card.Suit)}";

    #endregion
}
