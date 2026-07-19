using System.Collections.Generic;

namespace Content.Shared._Crescent.CartridgeLoader.Cartridges;

/// <summary>
/// Odds, paytables and hand maths shared by the client (paytable display) and the
/// server (authoritative resolution). The odds are rigged in the player's favour but kept
/// economy-safe: every bet wins with probability 1 - <see cref="LossChance"/> (~95%) and loses
/// otherwise. A win returns only the stake plus a small <see cref="WinProfit"/>, so the rare
/// full-stake losses roughly cancel the many small wins and gambling stays close to break-even.
/// The multiplier tables below no longer size payouts; they only sort a spin into win/push/loss.
/// </summary>
public static class GamblingTables
{
    /// <summary>
    /// Probability that any single bet is forced to lose. Every other bet is forced to win, so
    /// the player wins ~95% of the time. Bump this up for a harsher table, down for a kinder one.
    /// </summary>
    public const float LossChance = 0.05f;

    /// <summary>
    /// Profit paid on a win, as a fraction of the stake (a win returns stake × (1 + this)). Small
    /// on purpose: at a ~95% win rate this keeps the game near break-even so it cannot be farmed.
    /// With <see cref="LossChance"/> = 0.05, break-even is ~0.053, so this leaves a tiny edge.
    /// Raise it for a more generous table — but past ~0.1 the many wins start to outrun the losses.
    /// </summary>
    public const float WinProfit = 0.06f;

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

    /// <summary>
    /// Legacy paytable multiplier for a triple. Payouts no longer use it (a win pays a flat
    /// <see cref="WinProfit"/>); it now only sorts a spin into win (&gt;1) / push (1) / loss (0)
    /// so the reels shown match the rigged result.
    /// </summary>
    public static int SlotTripleMultiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 7,
        SlotSymbol.Lemon => 8,
        SlotSymbol.Bell => 13,
        SlotSymbol.Star => 27,
        SlotSymbol.Diamond => 52,
        SlotSymbol.Seven => 280,
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
    /// House edge removed: there are no green house pockets. Pockets 0-35 map straight onto
    /// numbers 1-36, so paying true odds gives every bet type an identical 100% return — the
    /// wheel is fair and no bet is better than another.
    /// </summary>
    public const int RoulettePocketCount = 36;

    public const int RouletteGreenCount = 0;

    public static bool IsGreen(int pocket) => pocket >= 0 && pocket < RouletteGreenCount;

    /// <summary>Returns the 1-36 number of a pocket.</summary>
    public static int PocketNumber(int pocket) => pocket - RouletteGreenCount + 1;

    public static string PocketLabel(int pocket) => PocketNumber(pocket).ToString();

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
        // No green pockets remain, but keep the guard so the maths stays correct if any return.
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
    /// Fair rules, house edge removed: the dealer stands on all 17s (soft included), blackjack
    /// pays 3:2, and a tie is a push that returns the stake. There is no doubling or splitting.
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
