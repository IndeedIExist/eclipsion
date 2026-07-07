namespace Content.Server.Crescent.Dispenser;

[RegisterComponent]
public sealed partial class StationTradeMarketComponent : Component
{
    [DataField]
    public Dictionary<string, float> SalesAccumulator = new();

    [DataField]
    public float PriceDropPerSale = 0.02f;

    [DataField]
    public float MinMultiplier = 0.3f;


    [DataField]
    public float RecoveryRatePerSecond = 1f / 60f;
}