using System.Linq;
using Content.Shared.Cargo.Cartridges;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Cargo.UI;

public sealed partial class StockMarketUiFragment : BoxContainer
{
    private static readonly Color UpColor = StockPriceChart.UpColor;
    private static readonly Color DownColor = StockPriceChart.DownColor;
    private static readonly Color NeutralColor = StockPriceChart.NeutralColor;

    private readonly Label _balanceLabel;
    private readonly OptionButton _amountSelector;
    private readonly TabContainer _tabs;
    private readonly BoxContainer _marketList;
    private readonly BoxContainer _portfolioList;
    private readonly BoxContainer _historyList;
    private readonly BoxContainer _newsList;
    private readonly Label _toastLabel;

    private const int AllAmount = -1;

    private static readonly int[] TradeAmounts = { 1, 5, 10, 25, AllAmount };
    private int _tradeAmount = 1;
    private int _lastHistoryCount = -1;
    private float _toastTimer;
    private StockMarketUiState? _lastState;

    public Action<string, int>? OnBuyPressed;
    public Action<string, int>? OnSellPressed;

    public StockMarketUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        Margin = new Thickness(4);

        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        header.AddChild(new Label
        {
            Text = Loc.GetString("stock-market-app-title"),
            StyleClasses = { "LabelHeading" },
            HorizontalExpand = true,
        });

        _balanceLabel = new Label
        {
            Text = Loc.GetString("stock-market-balance", ("balance", "—")),
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
        };
        header.AddChild(_balanceLabel);
        AddChild(header);

        var controlsRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2),
        };

        controlsRow.AddChild(new Label
        {
            Text = Loc.GetString("stock-market-qty"),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        });

        _amountSelector = new OptionButton();
        for (var i = 0; i < TradeAmounts.Length; i++)
        {
            var label = TradeAmounts[i] == AllAmount
                ? Loc.GetString("stock-market-qty-all")
                : $"x{TradeAmounts[i]}";
            _amountSelector.AddItem(label, i);
        }
        _amountSelector.OnItemSelected += args =>
        {
            _amountSelector.SelectId(args.Id);
            _tradeAmount = TradeAmounts[args.Id];

            if (_lastState != null)
                UpdateMarketTab(_lastState);
        };
        controlsRow.AddChild(_amountSelector);
        AddChild(controlsRow);

        _tabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _marketList = MakeTab("stock-market-tab-market", 0, out var marketScroll);
        _portfolioList = MakeTab("stock-market-tab-portfolio", 1, out var portfolioScroll);
        _historyList = MakeTab("stock-market-tab-history", 2, out var historyScroll);
        _newsList = MakeTab("stock-market-tab-news", 3, out var newsScroll);

        _tabs.AddChild(marketScroll);
        _tabs.AddChild(portfolioScroll);
        _tabs.AddChild(historyScroll);
        _tabs.AddChild(newsScroll);

        _tabs.SetTabTitle(0, Loc.GetString("stock-market-tab-market"));
        _tabs.SetTabTitle(1, Loc.GetString("stock-market-tab-portfolio"));
        _tabs.SetTabTitle(2, Loc.GetString("stock-market-tab-history"));
        _tabs.SetTabTitle(3, Loc.GetString("stock-market-tab-news"));
        AddChild(_tabs);

        _toastLabel = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 2),
            Visible = false,
        };
        AddChild(_toastLabel);
    }

    private static BoxContainer MakeTab(string locKey, int index, out ScrollContainer scroll)
    {
        var list = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        scroll.AddChild(list);
        return list;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_toastLabel.Visible)
            return;

        _toastTimer -= args.DeltaSeconds;
        if (_toastTimer <= 0f)
            _toastLabel.Visible = false;
    }

    private void ShowToast(string text, Color color)
    {
        _toastLabel.Text = text;
        _toastLabel.FontColorOverride = color;
        _toastLabel.Visible = true;
        _toastTimer = 4f;
    }

    public void UpdateState(StockMarketUiState state)
    {
        _lastState = state;

        _balanceLabel.Text = state.Balance is { } balance
            ? Loc.GetString("stock-market-balance", ("balance", $"{balance}cr"))
            : Loc.GetString("stock-market-balance", ("balance", "—"));

        UpdateMarketTab(state);
        UpdatePortfolioTab(state);
        UpdateHistoryTab(state);
        UpdateNewsTab(state);

        if (_lastHistoryCount >= 0 && state.History.Count > _lastHistoryCount)
        {
            var trade = state.History[^1];
            var name = Loc.GetString(trade.CompanyId);
            var key = trade.IsBuy ? "stock-market-toast-bought" : "stock-market-toast-sold";
            var total = trade.Amount * trade.PricePerShare;
            ShowToast(
                Loc.GetString(key, ("amount", trade.Amount), ("company", name), ("total", $"{total:F0}")),
                trade.IsBuy ? UpColor : DownColor);
        }
        _lastHistoryCount = state.History.Count;
    }

    private void UpdateMarketTab(StockMarketUiState state)
    {
        _marketList.RemoveAllChildren();

        if (state.Prices.Count == 0)
        {
            _marketList.AddChild(new Label
            {
                Text = Loc.GetString("economic-console-no-data"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 8),
            });
            return;
        }

        var stocks = state.Prices
            .OrderBy(p => Loc.GetString(p.Key))
            .ToList();

        foreach (var (id, price) in stocks)
        {
            _marketList.AddChild(CreateStockRow(id, price, state));
        }
    }

    private Control CreateStockRow(string id, StockPriceData price, StockMarketUiState state)
    {
        var owned = state.Portfolio.GetValueOrDefault(id, 0);

        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(0, 2),
        };

        var infoRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var displayName = Loc.GetString(id);
        infoRow.AddChild(new Label
        {
            Text = TruncateString(displayName, 20),
            HorizontalExpand = true,
            StyleClasses = { "LabelHeading" },
        });

        var trendColor = price.PriceChange > 0 ? UpColor
            : price.PriceChange < 0 ? DownColor
            : NeutralColor;

        var changeText = price.PriceChange >= 0 ? "▲" : "▼";
        infoRow.AddChild(new Label
        {
            Text = $"{price.CurrentPrice:F0}cr {changeText}{Math.Abs(price.PriceChange * 100):F1}%",
            HorizontalAlignment = HAlignment.Right,
            FontColorOverride = trendColor,
        });
        container.AddChild(infoRow);

        if (price.PriceHistory is { Count: > 1 })
        {
            var chart = new StockPriceChart
            {
                HorizontalExpand = true,
                MinHeight = 42,
                Margin = new Thickness(0, 2),
            };
            chart.SetData(price.PriceHistory, (float) price.BasePrice);
            container.AddChild(chart);
        }

        var actionRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        actionRow.AddChild(new Label
        {
            Text = Loc.GetString("stock-market-owned") + $": {owned}",
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        var sellAmount = _tradeAmount;
        var buyAmount = _tradeAmount;
        if (_tradeAmount == AllAmount)
        {
            sellAmount = Math.Min(owned, StockMarketTrading.MaxStockAmount);

            var perShare = price.CurrentPrice;
            buyAmount = state.Balance is { } b && perShare > 0
                ? (int) Math.Clamp(Math.Floor(b / perShare), 0, StockMarketTrading.MaxStockAmount)
                : 0;
        }

        var proceeds = price.CurrentPrice * sellAmount;
        var sellButton = new Button
        {
            Text = Loc.GetString("stock-market-sell-btn"),
            MinWidth = 50,
            Disabled = sellAmount <= 0 || owned < sellAmount,
            ToolTip = $"x{sellAmount} = {proceeds:F0}cr",
        };
        var sellCapture = sellAmount;
        sellButton.OnPressed += _ => OnSellPressed?.Invoke(id, sellCapture);

        var cost = price.CurrentPrice * buyAmount;
        var buyButton = new Button
        {
            Text = Loc.GetString("stock-market-buy-btn"),
            MinWidth = 50,
            Disabled = buyAmount <= 0 || (state.Balance is { } bal && bal < cost),
            ToolTip = $"x{buyAmount} = {cost:F0}cr",
        };
        var buyCapture = buyAmount;
        buyButton.OnPressed += _ => OnBuyPressed?.Invoke(id, buyCapture);

        actionRow.AddChild(sellButton);
        actionRow.AddChild(buyButton);
        container.AddChild(actionRow);

        container.AddChild(new PanelContainer
        {
            MinHeight = 1,
            Margin = new Thickness(0, 4),
            StyleClasses = { "LowDivider" },
        });

        return container;
    }

    private void UpdatePortfolioTab(StockMarketUiState state)
    {
        _portfolioList.RemoveAllChildren();

        var holdings = state.Portfolio
            .Where(p => p.Value > 0)
            .OrderBy(p => Loc.GetString(p.Key))
            .ToList();

        if (holdings.Count == 0)
        {
            _portfolioList.AddChild(new Label
            {
                Text = Loc.GetString("stock-market-no-holdings"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 8),
            });
            return;
        }

        var totalValue = 0.0;
        foreach (var (id, shares) in holdings)
        {
            if (state.Prices.TryGetValue(id, out var price))
                totalValue += shares * price.CurrentPrice;
        }

        _portfolioList.AddChild(new Label
        {
            Text = Loc.GetString("stock-market-total-value", ("value", $"{totalValue:F0}")),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(0, 0, 0, 4),
        });

        foreach (var (id, shares) in holdings)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 1),
            };

            row.AddChild(new Label
            {
                Text = TruncateString(Loc.GetString(id), 20),
                HorizontalExpand = true,
            });

            // A holding with no quote belongs to a faction that is not fielded this round. It is still
            // owned and still carries over, it just cannot be traded until that faction is back.
            var valueText = state.Prices.TryGetValue(id, out var price)
                ? $"{shares} × {price.CurrentPrice:F0}cr = {shares * price.CurrentPrice:F0}cr"
                : Loc.GetString("stock-market-delisted", ("shares", shares));

            row.AddChild(new Label
            {
                Text = valueText,
                HorizontalAlignment = HAlignment.Right,
            });

            _portfolioList.AddChild(row);
        }
    }

    private void UpdateHistoryTab(StockMarketUiState state)
    {
        _historyList.RemoveAllChildren();

        if (state.History.Count == 0)
        {
            _historyList.AddChild(new Label
            {
                Text = Loc.GetString("stock-market-no-trades"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 8),
            });
            return;
        }

        for (var i = state.History.Count - 1; i >= 0; i--)
        {
            var trade = state.History[i];

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 1),
            };

            var verb = Loc.GetString(trade.IsBuy ? "stock-market-history-buy" : "stock-market-history-sell");
            row.AddChild(new Label
            {
                Text = $"[{trade.Time:hh\\:mm}] {verb} {trade.Amount} {TruncateString(Loc.GetString(trade.CompanyId), 14)}",
                HorizontalExpand = true,
                FontColorOverride = trade.IsBuy ? UpColor : DownColor,
            });

            row.AddChild(new Label
            {
                Text = $"@{trade.PricePerShare:F0}cr",
                HorizontalAlignment = HAlignment.Right,
            });

            _historyList.AddChild(row);
        }
    }

    /// <summary>
    /// The feed that tells a trader *why* a price moved. Without it the faction stocks look like
    /// random noise, when in fact they only ever move because of something that happened in the round.
    /// </summary>
    private void UpdateNewsTab(StockMarketUiState state)
    {
        _newsList.RemoveAllChildren();

        if (state.News.Count == 0)
        {
            _newsList.AddChild(new Label
            {
                Text = Loc.GetString("stock-market-no-news"),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 8),
            });
            return;
        }

        for (var i = state.News.Count - 1; i >= 0; i--)
        {
            var entry = state.News[i];

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 1),
            };

            row.AddChild(new Label
            {
                Text = $"{(entry.Positive ? "▲" : "▼")} {TruncateString(Loc.GetString(entry.CompanyId), 16)}",
                FontColorOverride = entry.Positive ? UpColor : DownColor,
                MinWidth = 110,
            });

            row.AddChild(new Label
            {
                Text = entry.Reason,
                HorizontalExpand = true,
            });

            _newsList.AddChild(row);
        }
    }

    private static string TruncateString(string str, int maxLength)
    {
        return str.Length <= maxLength ? str : str[..(maxLength - 2)] + "..";
    }
}
