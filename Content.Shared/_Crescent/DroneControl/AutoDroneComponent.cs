using Robust.Shared.Map;

namespace Content.Shared._Crescent.DroneControl;

/// <summary>
///     Put on a drone's control server (the entity that steers the drone grid). Makes the drone
///     automatically undock from its carrier, hold an assigned formation slot behind it, and
///     engage diplomatic enemies within the carrier's engagement range.
///     Manual orders from a drone control console temporarily override this autopilot.
/// </summary>
[RegisterComponent]
public sealed partial class AutoDroneComponent : Component
{
    /// <summary>
    ///     The carrier console this drone has been deployed from, or null if not yet deployed.
    ///     The carrier grid is derived from this console's grid.
    /// </summary>
    [ViewVariables]
    public EntityUid? CarrierConsole;

    /// <summary>
    ///     Assigned formation slot index on the carrier, or -1 if unassigned.
    /// </summary>
    [ViewVariables]
    public int Slot = -1;

    /// <summary>
    ///     Formation slot target, expressed relative to the carrier grid so it rotates and moves
    ///     with the carrier. Resolved live by the steering system each tick.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates SlotCoordinates;

    /// <summary>
    ///     Current behavior mode, for debugging/visibility.
    /// </summary>
    [ViewVariables]
    public AutoDroneMode Mode = AutoDroneMode.Idle;

    // ---- manual override ----

    /// <summary>
    ///     Until this time, the drone obeys the manual console order instead of the autopilot.
    /// </summary>
    [ViewVariables]
    public TimeSpan ManualOverrideUntil = TimeSpan.Zero;

    /// <summary>
    ///     The last manual command sent from a drone control console (drone_cmd_move / drone_cmd_target).
    /// </summary>
    [ViewVariables]
    public string? ManualCommand;

    /// <summary>
    ///     The target of the last manual command.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates ManualTarget;

    /// <summary>
    ///     How long a manual console order suspends the autopilot before it resumes.
    /// </summary>
    [DataField]
    public TimeSpan ManualOverrideTimeout = TimeSpan.FromSeconds(12);
}

public enum AutoDroneMode : byte
{
    Idle,
    Launching,
    Follow,
    Attack,
    Manual,
}
