using Content.Shared._Crescent.Economy;
using Content.Shared.Shipyard.Prototypes;

namespace Content.Client._Crescent.Economy;

/// <summary>
/// Client-side cache of runtime economy price overrides.
/// </summary>
public sealed class EconomyPriceSystem : EntitySystem
{
    private readonly Dictionary<string, double> _itemOverrides = new();
    private readonly Dictionary<string, int> _vesselOverrides = new();

    public event Action? PricesUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<EconomyPriceSyncEvent>(OnSync);
        SubscribeNetworkEvent<EconomyAdminListEvent>(OnListReceived);
        SubscribeNetworkEvent<EconomyAdminPriceUpdatedEvent>(OnPriceUpdated);
    }

    public event Action<EconomyAdminListEvent>? ListReceived;

    /// <summary>Raised when the server confirms a price/balance change, carrying the authoritative value.</summary>
    public event Action<EconomyAdminPriceUpdatedEvent>? PriceUpdated;

    public void RequestList(EconomyListCategory category, string searchFilter)
    {
        RaiseNetworkEvent(new EconomyAdminRequestListEvent
        {
            Category = category,
            SearchFilter = searchFilter,
        });
    }

    public void SetPrice(EconomyListCategory category, string id, double price)
    {
        RaiseNetworkEvent(new EconomyAdminSetPriceEvent
        {
            Category = category,
            Id = id,
            Price = price,
        });
    }

    public double GetEffectiveItemPrice(string protoId, double basePrice) =>
        _itemOverrides.TryGetValue(protoId, out var price) ? price : basePrice;

    public int GetVesselPrice(VesselPrototype vessel) =>
        _vesselOverrides.TryGetValue(vessel.ID, out var price) ? price : vessel.Price;

    private void OnSync(EconomyPriceSyncEvent msg, EntitySessionEventArgs args)
    {
        _itemOverrides.Clear();
        foreach (var (id, price) in msg.ItemOverrides)
            _itemOverrides[id] = price;

        _vesselOverrides.Clear();
        foreach (var (id, price) in msg.VesselOverrides)
            _vesselOverrides[id] = price;

        PricesUpdated?.Invoke();
    }

    private void OnListReceived(EconomyAdminListEvent msg, EntitySessionEventArgs args)
    {
        ListReceived?.Invoke(msg);
    }

    private void OnPriceUpdated(EconomyAdminPriceUpdatedEvent msg, EntitySessionEventArgs args)
    {
        PriceUpdated?.Invoke(msg);
    }
}
