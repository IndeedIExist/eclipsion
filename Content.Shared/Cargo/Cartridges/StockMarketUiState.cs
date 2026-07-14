using Content.Shared.CartridgeLoader;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Cartridges;

public static class StockMarketTrading
{
    public const int MaxStockAmount = 100000;
}

[Serializable, NetSerializable]
public sealed class StockMarketUiState : BoundUserInterfaceState
{
    public Dictionary<string, StockPriceData> Prices { get; init; }
    public Dictionary<string, int> Portfolio { get; init; }

    public long? Balance { get; init; }

    public List<StockTradeRecord> History { get; init; }

    public StockMarketUiState(
        Dictionary<string, StockPriceData> prices,
        Dictionary<string, int> portfolio,
        long? balance = null,
        List<StockTradeRecord>? history = null)
    {
        Prices = prices;
        Portfolio = portfolio;
        Balance = balance;
        History = history ?? new List<StockTradeRecord>();
    }
}

[Serializable, NetSerializable]
public sealed class StockMarketUiMessageEvent : CartridgeMessageEvent
{
    public StockMarketUiAction Action { get; init; }
    public string CompanyId { get; init; }
    public int Amount { get; init; }

    public StockMarketUiMessageEvent(StockMarketUiAction action, string companyId, int amount)
    {
        Action = action;
        CompanyId = companyId;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public enum StockMarketUiAction
{
    RequestPrices,
    Buy,
    Sell
}
