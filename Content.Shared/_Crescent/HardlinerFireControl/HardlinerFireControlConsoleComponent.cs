// Ratgore Start
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.HardlinerFireControl;

/// <summary>
/// Shuttle-mounted telemetry station for linked NT-9 3100c plasma lances.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HardlinerFireControlConsoleComponent : Component
{
    [DataField]
    public TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);
}

[Serializable, NetSerializable]
public enum HardlinerFireControlConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class HardlinerTelemetryRowState
{
    public string DeviceAddress = string.Empty;

    /// <summary>Stored energy fraction toward one capacitor discharge (0–1).</summary>
    public float CapacitorFraction;

    public int CapacitorJoules;

    public int ShotEnergyJoules;

    public int ShotsRemaining;

    public int ShotsCapacity;

    public bool GridPowered;

    public bool Armed;

    /// <summary>Burst / bore cooldown readiness (0 = cycling, 1 = ready).</summary>
    public float RecycleFraction;
}

[Serializable, NetSerializable]
public sealed class HardlinerFireControlConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<HardlinerTelemetryRowState> Rows = new();

    public HardlinerFireControlConsoleBoundUserInterfaceState(List<HardlinerTelemetryRowState> rows)
    {
        Rows = rows;
    }
}

[Serializable, NetSerializable]
public sealed class HardlinerFireControlConsoleBoundUserInterfaceMessage : BoundUserInterfaceMessage
{
    public List<HardlinerTelemetryRowState> Rows = new();

    public HardlinerFireControlConsoleBoundUserInterfaceMessage(List<HardlinerTelemetryRowState> rows)
    {
        Rows = rows;
    }
}
// Ratgore End
