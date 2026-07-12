// Ratgore Start
namespace Content.Shared._Crescent.HardlinerFireControl;

/// <summary>
/// Device network payloads exchanged between <see cref="HardlinerFireControlConsoleComponent"/> and Hardliner weapons.
/// </summary>
public static class HardlinerTelemetryCommands
{
    public const string CmdRequest = "hardliner_telemetry_request";

    public const string CmdResponse = "hardliner_telemetry_response";

    public const string KeyCapacitorFraction = "cap_frac";

    public const string KeyCapacitorJoules = "cap_j";

    public const string KeyShotEnergyJoules = "shot_j";

    public const string KeyShotsRemaining = "shots_now";

    public const string KeyShotsCapacity = "shots_max";

    public const string KeyGridPowered = "grid_power";

    public const string KeyArmed = "armed";

    public const string KeyRecycleFraction = "recycle_frac";
}
// Ratgore End
