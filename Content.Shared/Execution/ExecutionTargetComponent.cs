using Robust.Shared.GameStates;

namespace Content.Shared.Execution;

/// <summary>
/// Added to a victim while someone is channeling a finish-off (execution) on them. This is purely a
/// visual marker: while it is present, the client draws a pulsing red indicator with a label above
/// the entity so everyone can see the kill is being lined up. It is added when the execution
/// do-after starts and removed as soon as it completes or is interrupted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExecutionTargetComponent : Component
{
    /// <summary>
    /// Localized text shown above the victim while they are being finished off.
    /// </summary>
    [DataField]
    public LocId Text = "execution-finish-off-marker";
}
