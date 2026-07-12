using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Research.TechShare;

/// <summary>
/// Rental mode for tech sharing.
/// </summary>
[NetSerializable, Serializable]
public enum TechShareRentalMode : byte
{
    /// <summary>
    /// Time-based rental - recipes expire after a duration.
    /// </summary>
    Time,

    /// <summary>
    /// Production-based rental - recipes expire after X items are produced.
    /// </summary>
    Production
}

/// <summary>
/// Console that initiates tech sharing connections to receiver consoles.
/// Linked to the station's RnD server. Shinogara uses this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TechShareRequestComponent : Component
{
    /// <summary>
    /// The receiver console we are currently connected to, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ConnectedReceiver;

    /// <summary>
    /// The receiver console we have sent a pending request to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PendingTarget;

    /// <summary>
    /// When the current connection expires (for time-based rental).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? ConnectionExpiry;

    /// <summary>
    /// Recipes selected for sharing in the current/pending request.
    /// </summary>
    [DataField]
    public List<string> SelectedRecipes = new();

    /// <summary>
    /// Rental mode for the connection.
    /// </summary>
    [DataField]
    public TechShareRentalMode RentalMode = TechShareRentalMode.Time;

    /// <summary>
    /// Duration chosen by the requester for pending/active connection (in seconds).
    /// </summary>
    [DataField]
    public float PendingDurationSeconds = 600f;

    /// <summary>
    /// Production count chosen by the requester for pending/active connection.
    /// </summary>
    [DataField]
    public int PendingProductionCount = 1;

    /// <summary>
    /// Minimum allowed duration in minutes.
    /// </summary>
    [DataField]
    public int MinDurationMinutes = 1;

    /// <summary>
    /// Maximum allowed duration in minutes.
    /// </summary>
    [DataField]
    public int MaxDurationMinutes = 30;

    /// <summary>
    /// Minimum allowed production count.
    /// </summary>
    [DataField]
    public int MinProductionCount = 1;

    /// <summary>
    /// Maximum allowed production count.
    /// </summary>
    [DataField]
    public int MaxProductionCount = 10;

    /// <summary>
    /// The name shown in UI for this console.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ConsoleName = "TECH-REQ";
}

[NetSerializable, Serializable]
public enum TechShareRequestUiKey : byte
{
    Key,
}

/// <summary>
/// UI state sent to requester console.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareRequestBuiState : BoundUserInterfaceState
{
    public List<(NetEntity Uid, string Name)> AvailableReceivers;
    public List<string> AvailableRecipes;
    public NetEntity? PendingTarget;
    public NetEntity? ConnectedReceiver;
    public string? ConnectedReceiverName;
    public TimeSpan? TimeRemaining;
    public List<string>? SharedRecipeIds;
    public int MinDurationMinutes;
    public int MaxDurationMinutes;
    public int MinProductionCount;
    public int MaxProductionCount;

    public TechShareRequestBuiState(
        List<(NetEntity, string)> availableReceivers,
        List<string> availableRecipes,
        NetEntity? pendingTarget,
        NetEntity? connectedReceiver,
        string? connectedReceiverName,
        TimeSpan? timeRemaining,
        List<string>? sharedRecipeIds,
        int minDurationMinutes,
        int maxDurationMinutes,
        int minProductionCount,
        int maxProductionCount)
    {
        AvailableReceivers = availableReceivers;
        AvailableRecipes = availableRecipes;
        PendingTarget = pendingTarget;
        ConnectedReceiver = connectedReceiver;
        ConnectedReceiverName = connectedReceiverName;
        TimeRemaining = timeRemaining;
        SharedRecipeIds = sharedRecipeIds;
        MinDurationMinutes = minDurationMinutes;
        MaxDurationMinutes = maxDurationMinutes;
        MinProductionCount = minProductionCount;
        MaxProductionCount = maxProductionCount;
    }
}

/// <summary>
/// Player wants to send a connection request to a receiver with selected recipes and rental settings.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareSendRequestMessage : BoundUserInterfaceMessage
{
    public NetEntity TargetReceiver;
    public List<string> SelectedRecipes;
    public TechShareRentalMode RentalMode;
    public int DurationOrCount;

    public TechShareSendRequestMessage(NetEntity targetReceiver, List<string> selectedRecipes, TechShareRentalMode rentalMode, int durationOrCount)
    {
        TargetReceiver = targetReceiver;
        SelectedRecipes = selectedRecipes;
        RentalMode = rentalMode;
        DurationOrCount = durationOrCount;
    }
}

/// <summary>
/// Player wants to cancel a pending request.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareCancelRequestMessage : BoundUserInterfaceMessage;

/// <summary>
/// Player wants to disconnect an active connection.
/// </summary>
[Serializable, NetSerializable]
public sealed class TechShareDisconnectRequestMessage : BoundUserInterfaceMessage;
