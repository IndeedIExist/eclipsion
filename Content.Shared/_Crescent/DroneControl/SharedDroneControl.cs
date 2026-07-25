using Content.Shared.Crescent.Radar;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.DroneControl;

[Serializable, NetSerializable]
public enum DroneConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DroneConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public IFFInterfaceState IFFState;

    // Key: NetEntity of the drone, Value: Name
    public List<(NetEntity Server, NetEntity Grid)> LinkedDrones;

    // Carrier controls. IsCarrier false => hide the carrier control panel.
    public bool IsCarrier;
    public DroneStance Stance;
    public DroneTargeting Targeting;
    public DroneFormation Formation;
    public int DeployedCount;
    public int MaxDrones;
    public List<string> SpawnableDrones;

    public DroneConsoleBoundUserInterfaceState(
        NavInterfaceState navState,
        IFFInterfaceState iffState,
        List<(NetEntity, NetEntity)> linkedDrones,
        bool isCarrier,
        DroneStance stance,
        DroneTargeting targeting,
        DroneFormation formation,
        int deployedCount,
        int maxDrones,
        List<string> spawnableDrones)
    {
        NavState = navState;
        IFFState = iffState;
        LinkedDrones = linkedDrones;
        IsCarrier = isCarrier;
        Stance = stance;
        Targeting = targeting;
        Formation = formation;
        DeployedCount = deployedCount;
        MaxDrones = maxDrones;
        SpawnableDrones = spawnableDrones;
    }
}

/// <summary>
///     Sent when the player picks the drones' targeting scope (enemies only / all non-friendly).
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleSetTargetingMessage : BoundUserInterfaceMessage
{
    public DroneTargeting Targeting;

    public DroneConsoleSetTargetingMessage(DroneTargeting targeting)
    {
        Targeting = targeting;
    }
}

/// <summary>
///     Sent when the player produces a drone of the given vessel prototype id from the carrier console.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleSpawnMessage : BoundUserInterfaceMessage
{
    public string VesselId;

    public DroneConsoleSpawnMessage(string vesselId)
    {
        VesselId = vesselId;
    }
}

/// <summary>
///     Sent when the player picks a combat stance for the carrier's drones.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleSetStanceMessage : BoundUserInterfaceMessage
{
    public DroneStance Stance;

    public DroneConsoleSetStanceMessage(DroneStance stance)
    {
        Stance = stance;
    }
}

/// <summary>
///     Sent when the player picks a formation for the carrier's drones.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleSetFormationMessage : BoundUserInterfaceMessage
{
    public DroneFormation Formation;

    public DroneConsoleSetFormationMessage(DroneFormation formation)
    {
        Formation = formation;
    }
}

/// <summary>
///     Sent when the player asks the carrier to deploy its docked drones.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleDeployMessage : BoundUserInterfaceMessage
{
}

/// <summary>
///     Sent when the client determines the click was in empty space.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleMoveMessage : BoundUserInterfaceMessage
{
    public HashSet<NetEntity> SelectedDrones;
    public NetCoordinates TargetCoordinates;

    public DroneConsoleMoveMessage(HashSet<NetEntity> selectedDrones, NetCoordinates targetCoordinates)
    {
        SelectedDrones = selectedDrones;
        TargetCoordinates = targetCoordinates;
    }
}

/// <summary>
///     Sent when the client determines the click hit a grid.
/// </summary>
[Serializable, NetSerializable]
public sealed class DroneConsoleTargetMessage : BoundUserInterfaceMessage
{
    public HashSet<NetEntity> SelectedDrones;
    public NetCoordinates TargetCoordinates;

    public DroneConsoleTargetMessage(HashSet<NetEntity> selectedDrones, NetCoordinates targetCoordinates)
    {
        SelectedDrones = selectedDrones;
        TargetCoordinates = targetCoordinates;
    }
}

/// <summary>
///     Constants for DeviceNetwork packet keys.
/// </summary>
public static class DroneConsoleConstants
{
    public const string CommandMove = "drone_cmd_move";
    public const string CommandTarget = "drone_cmd_target";
    public const string TargetCoords = "target";
}

public enum DroneOrderType
{
    Move,
    Target
}
