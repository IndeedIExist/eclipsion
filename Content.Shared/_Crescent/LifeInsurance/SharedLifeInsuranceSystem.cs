using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.LifeInsurance;

[Serializable, NetSerializable]
public sealed class LifeInsuranceConsoleState : BoundUserInterfaceState
{
    public readonly int StoredProteins;
    public readonly int RequiredProteins;
    public readonly List<LifeInsuranceTargetEntry> Targets;
    public readonly List<LifeInsuranceSpawnMachineEntry> SpawnMachines;

    public LifeInsuranceConsoleState(
        int storedProteins,
        int requiredProteins,
        List<LifeInsuranceTargetEntry> targets,
        List<LifeInsuranceSpawnMachineEntry> spawnMachines)
    {
        StoredProteins = storedProteins;
        RequiredProteins = requiredProteins;
        Targets = targets;
        SpawnMachines = spawnMachines;
    }
}

[Serializable, NetSerializable]
public sealed class LifeInsuranceSpawnMachineEntry
{
    public readonly NetEntity Entity;
    public readonly string Name;

    public LifeInsuranceSpawnMachineEntry(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class LifeInsuranceTargetEntry
{
    public readonly NetEntity Target;
    public readonly string Name;
    public readonly string RoleName;
    public readonly bool AlreadyInsured;
    public readonly bool HasPendingRespawn;
    /// <summary>Credits charged from the operator when purchasing a policy for this mind (scales with RespawnCount).</summary>
    public readonly int NextCreditsPrice;
    /// <summary>Insurance respawn machine bound to this mind, if any.</summary>
    public readonly NetEntity? BoundSpawnMachine;

    public LifeInsuranceTargetEntry(
        NetEntity target,
        string name,
        string roleName,
        bool alreadyInsured,
        bool hasPendingRespawn,
        int nextCreditsPrice,
        NetEntity? boundSpawnMachine)
    {
        Target = target;
        Name = name;
        RoleName = roleName;
        AlreadyInsured = alreadyInsured;
        HasPendingRespawn = hasPendingRespawn;
        NextCreditsPrice = nextCreditsPrice;
        BoundSpawnMachine = boundSpawnMachine;
    }
}

[Serializable, NetSerializable]
public sealed class LifeInsuranceSelectTargetMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;
    /// <summary>Spawn machine to bind when issuing a policy; invalid to leave unchanged.</summary>
    public readonly NetEntity SpawnMachine;

    public LifeInsuranceSelectTargetMessage(NetEntity target, NetEntity spawnMachine = default)
    {
        Target = target;
        SpawnMachine = spawnMachine;
    }
}

[Serializable, NetSerializable]
public sealed class LifeInsuranceEjectProteinsMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LifeInsuranceVoidInsuranceMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;

    public LifeInsuranceVoidInsuranceMessage(NetEntity target)
    {
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class LifeInsuranceSetSpawnMachineMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Target;
    public readonly NetEntity SpawnMachine;

    public LifeInsuranceSetSpawnMachineMessage(NetEntity target, NetEntity spawnMachine)
    {
        Target = target;
        SpawnMachine = spawnMachine;
    }
}

[Serializable, NetSerializable]
public enum LifeInsuranceUiKey : byte
{
    Key
}
