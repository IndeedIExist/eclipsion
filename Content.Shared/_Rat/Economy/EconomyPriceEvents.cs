using Robust.Shared.Serialization;

namespace Content.Shared._Rat.Economy;

[Serializable, NetSerializable]
public enum EconomyPriceKind : byte
{
    Static,
    Stack,
    Vend,
    Mob,
}

[Serializable, NetSerializable]
public enum EconomyListCategory : byte
{
    Items,
    Vessels,
}

[Serializable, NetSerializable]
public sealed class EconomyPriceEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public EconomyListCategory Category;
    public EconomyPriceKind? PriceKind;
    public double BasePrice;
    public double CurrentPrice;

    public EconomyPriceEntry()
    {
    }

    public EconomyPriceEntry(
        string id,
        string name,
        EconomyListCategory category,
        EconomyPriceKind? priceKind,
        double basePrice,
        double currentPrice)
    {
        Id = id;
        Name = name;
        Category = category;
        PriceKind = priceKind;
        BasePrice = basePrice;
        CurrentPrice = currentPrice;
    }
}

[Serializable, NetSerializable]
public sealed class EconomyAdminRequestListEvent : EntityEventArgs
{
    public EconomyListCategory Category;
    public string SearchFilter = string.Empty;
}

[Serializable, NetSerializable]
public sealed class EconomyAdminListEvent : EntityEventArgs
{
    public EconomyListCategory Category;
    public List<EconomyPriceEntry> Entries = new();
}

[Serializable, NetSerializable]
public sealed class EconomyAdminSetPriceEvent : EntityEventArgs
{
    public EconomyListCategory Category;
    public string Id = string.Empty;
    public double Price;
}

[Serializable, NetSerializable]
public sealed class EconomyAdminPriceUpdatedEvent : EntityEventArgs
{
    public EconomyListCategory Category;
    public string Id = string.Empty;
    public double Price;
}

[Serializable, NetSerializable]
public sealed class EconomyPriceSyncEvent : EntityEventArgs
{
    public Dictionary<string, double> ItemOverrides = new();
    public Dictionary<string, int> VesselOverrides = new();
}
