using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Factions;

/// <summary>
/// A free "basic kit" vendor for a faction: it hands out a fixed set of items, each obtainable once per player.
/// Intended so members who changed factions or lost their gear can re-equip the essentials (radio, uniform,
/// jacket, basic faction kit). Gate it with an <see cref="Content.Shared.Access.Components.AccessReaderComponent"/>
/// so only the owning faction's members can use it.
/// </summary>
[RegisterComponent]
public sealed partial class FactionEquipmentVendorComponent : Component
{
    /// <summary>Items offered, in display order. Each entity can be dispensed once per player.</summary>
    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    /// <summary>
    /// Per-player record of already-taken item prototype ids, keyed by the player's user id string so it
    /// survives death/new bodies. Server-only bookkeeping — this component is intentionally not networked.
    /// </summary>
    [DataField]
    public Dictionary<string, HashSet<string>> Claimed = new();
}

[Serializable, NetSerializable]
public enum FactionVendorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FactionVendorState : BoundUserInterfaceState
{
    public readonly List<FactionVendorItem> Items;

    public FactionVendorState(List<FactionVendorItem> items)
    {
        Items = items;
    }
}

[Serializable, NetSerializable]
public sealed class FactionVendorItem
{
    public string ItemId = default!;
    public string Name = default!;
}

[Serializable, NetSerializable]
public sealed class FactionVendorTakeMessage : BoundUserInterfaceMessage
{
    public readonly string ItemId;

    public FactionVendorTakeMessage(string itemId)
    {
        ItemId = itemId;
    }
}
