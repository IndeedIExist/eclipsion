using System.Linq;
using Content.Shared._Crescent.Poker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Poker;

public sealed class PokerTableSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PokerTableComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PokerTableComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<PokerTableComponent, PokerJoinMessage>(OnJoin);
        SubscribeLocalEvent<PokerTableComponent, PokerLeaveMessage>(OnLeave);
        SubscribeLocalEvent<PokerTableComponent, PokerFoldMessage>(OnFold);
        SubscribeLocalEvent<PokerTableComponent, PokerCheckMessage>(OnCheck);
        SubscribeLocalEvent<PokerTableComponent, PokerCallMessage>(OnCall);
        SubscribeLocalEvent<PokerTableComponent, PokerBetMessage>(OnBet);
        SubscribeLocalEvent<PokerTableComponent, PokerRaiseMessage>(OnRaise);
        SubscribeLocalEvent<PokerTableComponent, PokerStartGameMessage>(OnStartGame);
    }

    private void OnUiOpened(EntityUid uid, PokerTableComponent comp, BoundUIOpenedEvent args)
    {
        SendState(uid, comp);
    }

    private void OnUiClosed(EntityUid uid, PokerTableComponent comp, BoundUIClosedEvent args)
    {
        var player = comp.Players.FirstOrDefault(p => p.Entity == args.Actor);
        if (player != null)
            RemovePlayer(uid, comp, player);
    }

    private void OnJoin(EntityUid uid, PokerTableComponent comp, PokerJoinMessage msg)
    {
        if (comp.Players.Any(p => p.Entity == msg.Actor))
            return;
        if (comp.Players.Count >= comp.MaxPlayers)
            return;
        if (comp.Phase != PokerRoundPhase.Waiting)
            return;

        var balance = ScanPlayerCash(msg.Actor);
        if (balance <= 0)
            return;

        var buyIn = Math.Min(balance, comp.StartingBuyIn);
        TakeCash(msg.Actor, buyIn);

        var name = Name(msg.Actor);
        var player = new PokerPlayer
        {
            Entity = msg.Actor,
            Name = name,
            Stack = buyIn,
            SeatIndex = comp.Players.Count
        };
        comp.Players.Add(player);
        SendState(uid, comp);
    }

    private void OnLeave(EntityUid uid, PokerTableComponent comp, PokerLeaveMessage msg)
    {
        var player = comp.Players.FirstOrDefault(p => p.Entity == msg.Actor);
        if (player != null)
            RemovePlayer(uid, comp, player);
    }

    private void RemovePlayer(EntityUid uid, PokerTableComponent comp, PokerPlayer player)
    {
        if (player.Stack > 0)
            GiveCash(player.Entity, player.Stack);

        comp.Players.Remove(player);

        for (var i = 0; i < comp.Players.Count; i++)
            comp.Players[i].SeatIndex = i;

        if (comp.Players.Count < comp.MinPlayers && comp.Phase != PokerRoundPhase.Waiting)
            EndRound(uid, comp);

        SendState(uid, comp);
    }

    private void OnStartGame(EntityUid uid, PokerTableComponent comp, PokerStartGameMessage msg)
    {
        if (comp.Phase != PokerRoundPhase.Waiting)
            return;
        if (comp.Players.Count < comp.MinPlayers)
            return;

        StartNewRound(uid, comp);
    }

    private void StartNewRound(EntityUid uid, PokerTableComponent comp)
    {
        comp.Deck = BuildAndShuffleDeck();
        comp.CommunityCards.Clear();
        comp.Pot = 0;
        comp.CurrentBet = 0;
        comp.LastRaiseAmount = comp.BigBlind;
        comp.Phase = PokerRoundPhase.PreFlop;
        comp.RoundNumber++;

        foreach (var p in comp.Players)
        {
            p.HoleCards.Clear();
            p.CurrentBet = 0;
            p.HasActed = false;
            p.Status = p.Stack > 0 ? PokerPlayerStatus.Active : PokerPlayerStatus.Folded;
        }

        var activePlayers = comp.Players.Where(p => p.Stack > 0).ToList();
        if (activePlayers.Count < 2)
        {
            EndRound(uid, comp);
            return;
        }

        comp.DealerIndex = comp.DealerIndex % activePlayers.Count;

        var sbIndex = (comp.DealerIndex + 1) % activePlayers.Count;
        var bbIndex = (comp.DealerIndex + 2) % activePlayers.Count;

        PostBlind(comp, activePlayers[sbIndex], comp.SmallBlind);
        PostBlind(comp, activePlayers[bbIndex], comp.BigBlind);

        comp.CurrentBet = comp.BigBlind;
        comp.CurrentPlayerIndex = (bbIndex + 1) % activePlayers.Count;

        foreach (var p in activePlayers)
        {
            p.HoleCards.Add(DealCard(comp));
            p.HoleCards.Add(DealCard(comp));
        }

        SendState(uid, comp);
    }

    private void PostBlind(PokerTableComponent comp, PokerPlayer player, int amount)
    {
        var actual = Math.Min(player.Stack, amount);
        player.Stack -= actual;
        player.CurrentBet += actual;
        comp.Pot += actual;
        if (player.Stack == 0)
            player.Status = PokerPlayerStatus.AllIn;
    }

    private void OnFold(EntityUid uid, PokerTableComponent comp, PokerFoldMessage msg)
    {
        if (!ValidateTurn(comp, msg.Actor, out var player))
            return;

        player.Status = PokerPlayerStatus.Folded;
        player.HasActed = true;
        AdvanceTurn(uid, comp);
    }

    private void OnCheck(EntityUid uid, PokerTableComponent comp, PokerCheckMessage msg)
    {
        if (!ValidateTurn(comp, msg.Actor, out var player))
            return;

        if (comp.CurrentBet > player.CurrentBet)
            return;

        player.HasActed = true;
        AdvanceTurn(uid, comp);
    }

    private void OnCall(EntityUid uid, PokerTableComponent comp, PokerCallMessage msg)
    {
        if (!ValidateTurn(comp, msg.Actor, out var player))
            return;

        var callAmount = comp.CurrentBet - player.CurrentBet;
        var actual = Math.Min(player.Stack, callAmount);

        player.Stack -= actual;
        player.CurrentBet += actual;
        comp.Pot += actual;

        if (player.Stack == 0)
            player.Status = PokerPlayerStatus.AllIn;

        player.HasActed = true;
        AdvanceTurn(uid, comp);
    }

    private void OnBet(EntityUid uid, PokerTableComponent comp, PokerBetMessage msg)
    {
        if (!ValidateTurn(comp, msg.Actor, out var player))
            return;
        if (comp.CurrentBet > 0)
            return;
        if (msg.Amount < comp.BigBlind || msg.Amount > player.Stack)
            return;

        var cashCheck = ScanPlayerCash(msg.Actor);
        var needed = msg.Amount - player.CurrentBet;
        if (needed > player.Stack)
            return;

        player.Stack -= needed;
        player.CurrentBet += needed;
        comp.Pot += needed;
        comp.CurrentBet = player.CurrentBet;
        comp.LastRaiseAmount = msg.Amount;

        if (player.Stack == 0)
            player.Status = PokerPlayerStatus.AllIn;

        foreach (var p in comp.Players)
            if (p != player && p.Status == PokerPlayerStatus.Active)
                p.HasActed = false;

        player.HasActed = true;
        AdvanceTurn(uid, comp);
    }

    private void OnRaise(EntityUid uid, PokerTableComponent comp, PokerRaiseMessage msg)
    {
        if (!ValidateTurn(comp, msg.Actor, out var player))
            return;

        var minRaise = comp.CurrentBet + comp.LastRaiseAmount;
        if (msg.Amount < minRaise && msg.Amount < player.Stack)
            return;

        var totalBet = Math.Min(msg.Amount, player.Stack + player.CurrentBet);
        var needed = totalBet - player.CurrentBet;

        if (needed > player.Stack)
            return;

        comp.LastRaiseAmount = totalBet - comp.CurrentBet;
        comp.CurrentBet = totalBet;

        player.Stack -= needed;
        player.CurrentBet = totalBet;
        comp.Pot += needed;

        if (player.Stack == 0)
            player.Status = PokerPlayerStatus.AllIn;

        foreach (var p in comp.Players)
            if (p != player && p.Status == PokerPlayerStatus.Active)
                p.HasActed = false;

        player.HasActed = true;
        AdvanceTurn(uid, comp);
    }

    private bool ValidateTurn(PokerTableComponent comp, EntityUid actor, out PokerPlayer player)
    {
        player = null!;
        if (comp.Phase == PokerRoundPhase.Waiting || comp.Phase == PokerRoundPhase.Showdown)
            return false;

        var active = comp.Players.Where(p => p.Status == PokerPlayerStatus.Active).ToList();
        if (active.Count == 0)
            return false;

        var current = active[comp.CurrentPlayerIndex % active.Count];
        if (current.Entity != actor)
            return false;

        player = current;
        return true;
    }

    private void AdvanceTurn(EntityUid uid, PokerTableComponent comp)
    {
        var active = comp.Players.Where(p => p.Status == PokerPlayerStatus.Active).ToList();

        if (active.Count <= 1)
        {
            EndRound(uid, comp);
            return;
        }

        var allActed = active.All(p => p.HasActed && p.CurrentBet == comp.CurrentBet);
        if (!allActed)
        {
            comp.CurrentPlayerIndex = (comp.CurrentPlayerIndex + 1) % active.Count;
            var next = active[comp.CurrentPlayerIndex % active.Count];
            while (next.Status != PokerPlayerStatus.Active)
            {
                comp.CurrentPlayerIndex = (comp.CurrentPlayerIndex + 1) % active.Count;
                next = active[comp.CurrentPlayerIndex % active.Count];
            }
            SendState(uid, comp);
            return;
        }

        AdvancePhase(uid, comp);
    }

    private void AdvancePhase(EntityUid uid, PokerTableComponent comp)
    {
        foreach (var p in comp.Players)
        {
            p.CurrentBet = 0;
            if (p.Status == PokerPlayerStatus.Active)
                p.HasActed = false;
        }
        comp.CurrentBet = 0;
        comp.LastRaiseAmount = comp.BigBlind;

        switch (comp.Phase)
        {
            case PokerRoundPhase.PreFlop:
                comp.Phase = PokerRoundPhase.Flop;
                comp.CommunityCards.Add(DealCard(comp));
                comp.CommunityCards.Add(DealCard(comp));
                comp.CommunityCards.Add(DealCard(comp));
                break;
            case PokerRoundPhase.Flop:
                comp.Phase = PokerRoundPhase.Turn;
                comp.CommunityCards.Add(DealCard(comp));
                break;
            case PokerRoundPhase.Turn:
                comp.Phase = PokerRoundPhase.River;
                comp.CommunityCards.Add(DealCard(comp));
                break;
            case PokerRoundPhase.River:
                comp.Phase = PokerRoundPhase.Showdown;
                DoShowdown(uid, comp);
                return;
        }

        var activePlayers = comp.Players.Where(p => p.Status == PokerPlayerStatus.Active).ToList();
        if (activePlayers.Count == 0)
        {
            EndRound(uid, comp);
            return;
        }
        comp.CurrentPlayerIndex = 0;

        SendState(uid, comp);
    }

    private void DoShowdown(EntityUid uid, PokerTableComponent comp)
    {
        var contenders = comp.Players.Where(p =>
            p.Status == PokerPlayerStatus.Active || p.Status == PokerPlayerStatus.AllIn).ToList();

        if (contenders.Count == 0)
        {
            EndRound(uid, comp);
            return;
        }

        PokerPlayer? winner = null;
        HandRank bestRank = HandRank.HighCard;
        List<PokerCard>? bestHand = null;

        foreach (var player in contenders)
        {
            var all = player.HoleCards.Concat(comp.CommunityCards).ToList();
            var (rank, hand) = EvaluateBestHand(all);
            if (winner == null || rank > bestRank ||
                (rank == bestRank && CompareHands(hand, bestHand!) > 0))
            {
                winner = player;
                bestRank = rank;
                bestHand = hand;
            }
        }

        if (winner != null)
        {
            winner.Stack += comp.Pot;
            winner.Status = PokerPlayerStatus.Winner;
        }

        comp.Pot = 0;
        SendState(uid, comp, winner?.Name, bestRank.ToString());

        var uid2 = uid;
        Timer.Spawn(5000, () =>
        {
            if (!EntityManager.EntityExists(uid2))
                return;
            if (!TryComp<PokerTableComponent>(uid2, out var c))
                return;

            comp.DealerIndex = (comp.DealerIndex + 1) % Math.Max(1, comp.Players.Count);
            var stillIn = comp.Players.Where(p => p.Stack > 0).ToList();
            if (stillIn.Count >= comp.MinPlayers)
                StartNewRound(uid2, c);
            else
            {
                c.Phase = PokerRoundPhase.Waiting;
                SendState(uid2, c);
            }
        });
    }

    private void EndRound(EntityUid uid, PokerTableComponent comp)
    {
        var remaining = comp.Players.Where(p =>
            p.Status == PokerPlayerStatus.Active || p.Status == PokerPlayerStatus.AllIn).ToList();

        if (remaining.Count == 1)
        {
            remaining[0].Stack += comp.Pot;
            remaining[0].Status = PokerPlayerStatus.Winner;
        }

        comp.Pot = 0;
        comp.Phase = PokerRoundPhase.Waiting;

        var toKick = comp.Players.Where(p => p.Stack == 0).ToList();
        foreach (var p in toKick)
            comp.Players.Remove(p);

        for (var i = 0; i < comp.Players.Count; i++)
            comp.Players[i].SeatIndex = i;

        SendState(uid, comp);
    }

    private void SendState(EntityUid uid, PokerTableComponent comp,
        string? winnerName = null, string? winningHand = null)
    {
        // Determine whose turn it is
        NetEntity? currentTurnEntity = null;
        var active = comp.Players.Where(p => p.Status == PokerPlayerStatus.Active).ToList();
        if (active.Count > 0
            && comp.Phase != PokerRoundPhase.Waiting
            && comp.Phase != PokerRoundPhase.Showdown)
        {
            currentTurnEntity = GetNetEntity(active[comp.CurrentPlayerIndex % active.Count].Entity);
        }

        // All players' hole cards are included; client shows only its own during game phase
        var playerStatesWithCards = comp.Players.Select(p => new PokerPlayerState
        {
            PlayerName = p.Name,
            Stack = p.Stack,
            CurrentBet = p.CurrentBet,
            Status = p.Status,
            HoleCards = p.HoleCards.Count > 0 ? new List<PokerCard>(p.HoleCards) : new List<PokerCard>(),
            IsCurrentTurn = currentTurnEntity.HasValue && GetNetEntity(p.Entity) == currentTurnEntity,
            SeatIndex = p.SeatIndex,
            PlayerEntity = GetNetEntity(p.Entity)
        }).ToList();

        var state = new PokerTableBoundUserInterfaceState
        {
            Players = playerStatesWithCards,
            CommunityCards = comp.CommunityCards,
            Pot = comp.Pot,
            Phase = comp.Phase,
            CurrentBet = comp.CurrentBet,
            MinRaise = comp.CurrentBet + comp.LastRaiseAmount,
            // These are placeholders — client overwrites with local entity data
            MyStack = 0,
            MyBet = 0,
            IsMyTurn = false,
            MySeatIndex = -1,
            BigBlind = comp.BigBlind,
            WinnerName = winnerName,
            WinningHand = winningHand,
            CurrentTurnEntity = currentTurnEntity
        };

        _ui.SetUiState(uid, PokerUiKey.Key, state);
    }

    private bool IsCurrentTurn(PokerTableComponent comp, EntityUid actor)
    {
        if (comp.Phase == PokerRoundPhase.Waiting || comp.Phase == PokerRoundPhase.Showdown)
            return false;
        var active = comp.Players.Where(p => p.Status == PokerPlayerStatus.Active).ToList();
        if (active.Count == 0)
            return false;
        return active[comp.CurrentPlayerIndex % active.Count].Entity == actor;
    }

    private PokerCard DealCard(PokerTableComponent comp)
    {
        var card = comp.Deck[^1];
        comp.Deck.RemoveAt(comp.Deck.Count - 1);
        return card;
    }

    private List<PokerCard> BuildAndShuffleDeck()
    {
        var deck = new List<PokerCard>();
        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            foreach (CardRank rank in Enum.GetValues(typeof(CardRank)))
                deck.Add(new PokerCard(suit, rank));

        var rng = new Random();
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }

    private int ScanPlayerCash(EntityUid player)
    {
        var total = 0;
        if (!_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
            return 0;
        while (enumerator.NextItem(out var item, out _))
        {
            total += CountCashInEntity(item);
        }
        return total;
    }

    private int CountCashInEntity(EntityUid entity)
    {
        var total = 0;

        if (TryComp<StackComponent>(entity, out var stack) &&
            stack.StackTypeId == "Credit")
        {
            total += stack.Count;
        }

        if (TryComp<Robust.Shared.Containers.ContainerManagerComponent>(entity, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    total += CountCashInEntity(contained);
                }
            }
        }
        return total;
    }

    private void TakeCash(EntityUid player, int amount)
    {
        if (amount <= 0) return;

        var remaining = amount;
        if (!_inventory.TryGetContainerSlotEnumerator(player, out var enumerator))
            return;
        while (enumerator.NextItem(out var item, out _) && remaining > 0)
        {
            remaining = TakeCashFromEntity(item, remaining);
        }
    }

    private int TakeCashFromEntity(EntityUid entity, int remaining)
    {
        if (remaining <= 0)
            return 0;

        if (TryComp<StackComponent>(entity, out var stack) && stack.StackTypeId == "Credit")
        {
            var take = Math.Min(stack.Count, remaining);
            _stack.SetCount(entity, stack.Count - take, stack);
            remaining -= take;
        }

        if (TryComp<Robust.Shared.Containers.ContainerManagerComponent>(entity, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities.ToList())
                {
                    remaining = TakeCashFromEntity(contained, remaining);
                    if (remaining <= 0) break;
                }
                if (remaining <= 0) break;
            }
        }
        return remaining;
    }

    private void GiveCash(EntityUid player, int amount)
    {
        if (amount <= 0) return;
        var cash = EntityManager.SpawnEntity("SpaceCash", Transform(player).Coordinates);
        if (TryComp<StackComponent>(cash, out var stack))
            _stack.SetCount(cash, amount, stack);
        // Try to put into hand; if hands full it stays at player's feet
        _hands.TryPickup(player, cash);
    }

    private (HandRank rank, List<PokerCard> bestFive) EvaluateBestHand(List<PokerCard> cards)
    {
        var best = (HandRank.HighCard, new List<PokerCard>());
        var combos = GetCombinations(cards, 5);

        foreach (var combo in combos)
        {
            var (rank, five) = EvaluateFiveCardHand(combo);
            if (rank > best.Item1 || (rank == best.Item1 && CompareHands(five, best.Item2) > 0))
                best = (rank, five);
        }
        return best;
    }

    private (HandRank, List<PokerCard>) EvaluateFiveCardHand(List<PokerCard> five)
    {
        var sorted = five.OrderByDescending(c => (int)c.Rank).ToList();
        var isFlush = sorted.All(c => c.Suit == sorted[0].Suit);
        var ranks = sorted.Select(c => (int)c.Rank).ToList();

        var isStraight = false;
        var straightHigh = 0;
        for (var i = 0; i < ranks.Count - 1; i++)
            if (ranks[i] - ranks[i + 1] != 1) { isStraight = false; break; }
            else isStraight = true;

        if (isStraight) straightHigh = ranks[0];

        if (!isStraight && ranks.SequenceEqual(new[] { 14, 5, 4, 3, 2 }))
        {
            isStraight = true;
            straightHigh = 5;
        }

        var groups = sorted.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => (int)g.Key).ToList();
        var counts = groups.Select(g => g.Count()).ToList();

        if (isFlush && isStraight)
            return straightHigh == 14 ? (HandRank.RoyalFlush, sorted) : (HandRank.StraightFlush, sorted);
        if (counts[0] == 4) return (HandRank.FourOfAKind, sorted);
        if (counts[0] == 3 && counts[1] == 2) return (HandRank.FullHouse, sorted);
        if (isFlush) return (HandRank.Flush, sorted);
        if (isStraight) return (HandRank.Straight, sorted);
        if (counts[0] == 3) return (HandRank.ThreeOfAKind, sorted);
        if (counts[0] == 2 && counts[1] == 2) return (HandRank.TwoPair, sorted);
        if (counts[0] == 2) return (HandRank.OnePair, sorted);
        return (HandRank.HighCard, sorted);
    }

    private int CompareHands(List<PokerCard> a, List<PokerCard> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var cmp = ((int)a[i].Rank).CompareTo((int)b[i].Rank);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    private IEnumerable<List<PokerCard>> GetCombinations(List<PokerCard> list, int k)
    {
        if (k == 0) { yield return new List<PokerCard>(); yield break; }
        for (var i = 0; i <= list.Count - k; i++)
            foreach (var rest in GetCombinations(list.Skip(i + 1).ToList(), k - 1))
            {
                var combo = new List<PokerCard> { list[i] };
                combo.AddRange(rest);
                yield return combo;
            }
    }
}
