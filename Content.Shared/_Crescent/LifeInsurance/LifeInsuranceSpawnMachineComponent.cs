using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.LifeInsurance;

/// <summary>
/// Marks an entity as a valid life-insurance respawn point (body is created at this machine).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LifeInsuranceSpawnMachineComponent : Component;
