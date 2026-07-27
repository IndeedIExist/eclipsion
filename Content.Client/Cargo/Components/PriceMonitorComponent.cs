namespace Content.Client.Cargo.Components;

public sealed class PriceMonitorData
{
    public Dictionary<string, PriceData> GoodsPrices = new();
    public Dictionary<string, ShuttlePriceData> ShuttlePrices = new();
}

[DataRecord]
public partial record struct PriceData(
    string GoodId,
    double BasePrice,
    double CurrentPrice,
    float Multiplier,
    float Trend
);

[DataRecord]
public partial record struct ShuttlePriceData(
    string ShuttleName,
    double BasePrice,
    double CurrentPrice,
    float PercentChange
);
