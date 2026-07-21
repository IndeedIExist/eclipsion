using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class LatheUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<LatheRecipePrototype>> Recipes;

    public List<LatheQueueEntry> Queue;

    public LatheRecipePrototype? CurrentlyProducing;

    public LatheUpdateState(List<ProtoId<LatheRecipePrototype>> recipes, List<LatheQueueEntry> queue, LatheRecipePrototype? currentlyProducing = null)
    {
        Recipes = recipes;
        Queue = queue;
        CurrentlyProducing = currentlyProducing;
    }
}

/// <summary>
/// A run of the same recipe queued back to back. Machines that get fed in bulk end up with hundreds of
/// identical jobs, and a queue sent as one entry per job means sending a whole recipe prototype per job,
/// several times a second, for as long as somebody has the window open.
/// </summary>
[Serializable, NetSerializable]
public struct LatheQueueEntry
{
    public ProtoId<LatheRecipePrototype> Recipe;

    public int Count;

    public LatheQueueEntry(ProtoId<LatheRecipePrototype> recipe, int count)
    {
        Recipe = recipe;
        Count = count;
    }
}

/// <summary>
///     Sent to the server to sync material storage and the recipe queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheSyncRequestMessage : BoundUserInterfaceMessage
{

}

/// <summary>
///     Sent to the server when a client queues a new recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheQueueRecipeMessage : BoundUserInterfaceMessage
{
    public readonly string ID;
    public readonly int Quantity;
    public LatheQueueRecipeMessage(string id, int quantity)
    {
        ID = id;
        Quantity = quantity;
    }
}

[NetSerializable, Serializable]
public enum LatheUiKey
{
    Key,
}
