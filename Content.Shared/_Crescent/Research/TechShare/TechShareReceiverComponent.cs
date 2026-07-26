using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Research.TechShare;

/// <summary>
/// Console that receives tech sharing requests from requester consoles.
/// Linked to the station's RnD server. Other factions use this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TechShareReceiverComponent : Component
{
    /// <summary>
    /// The requester console we are currently connected to, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ConnectedRequester;

    /// <summary>
    /// Incoming pending request from a requester, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PendingRequester;

    /// <summary>
    /// Recipes currently being shared to this receiver.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> SharedRecipes = new();

    /// <summary>
    /// Remaining production count for each shared recipe (for production-based rental).
    /// Key: recipe ID, Value: remaining count.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> RemainingProductionCounts = new();

    /// <summary>
    /// Rental mode for the active connection.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TechShareRentalMode RentalMode = TechShareRentalMode.Time;

    /// <summary>
    /// The name shown in UI for this console.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ConsoleName = "TECH-RCV";
}

[NetSerializable, Serializable]
public enum TechShareReceiverUiKey : byte
{
    Key,
}

/// <summary>
/// UI state sent to receiver console.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareReceiverBuiState : BoundUserInterfaceState
{
    public NetEntity? PendingRequester;
    public string? PendingRequesterName;
    public NetEntity? ConnectedRequester;
    public string? ConnectedRequesterName;
    public TimeSpan? TimeRemaining;
    public List<string>? OfferedRecipes;
    public int? OfferedDurationMinutes;
    public TechShareRentalMode RentalMode;
    public Dictionary<string, int>? RemainingProductionCounts;

    public TechShareReceiverBuiState(
        NetEntity? pendingRequester,
        string? pendingRequesterName,
        NetEntity? connectedRequester,
        string? connectedRequesterName,
        TimeSpan? timeRemaining,
        List<string>? offeredRecipes,
        int? offeredDurationMinutes,
        TechShareRentalMode rentalMode,
        Dictionary<string, int>? remainingProductionCounts)
    {
        PendingRequester = pendingRequester;
        PendingRequesterName = pendingRequesterName;
        ConnectedRequester = connectedRequester;
        ConnectedRequesterName = connectedRequesterName;
        TimeRemaining = timeRemaining;
        OfferedRecipes = offeredRecipes;
        OfferedDurationMinutes = offeredDurationMinutes;
        RentalMode = rentalMode;
        RemainingProductionCounts = remainingProductionCounts;
    }
}

/// <summary>
/// Player accepts a pending connection request.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareAcceptMessage : BoundUserInterfaceMessage;

/// <summary>
/// Player rejects a pending connection request.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareRejectMessage : BoundUserInterfaceMessage;

/// <summary>
/// Player disconnects the active connection from receiver side.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareDisconnectReceiverMessage : BoundUserInterfaceMessage;
