using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Silicon.Components;

/// <summary>
///     Plays a warning alarm to the Silicon's own player when its charge drops
///     past configured thresholds (e.g. 75%, 50%, 25%).
///     Only the Silicon itself hears the alarm, so it does not annoy everyone nearby.
/// </summary>
[RegisterComponent]
public sealed partial class SiliconChargeWarningComponent : Component
{
    /// <summary>
    ///     Charge thresholds (as a fraction of max charge, 0-1) that trigger a warning,
    ///     each with the sound and optional popup played when charge drops to/below it.
    /// </summary>
    [DataField]
    public List<SiliconChargeWarningThreshold> Thresholds = new();

    /// <summary>
    ///     Thresholds that have already fired and are waiting to be re-armed by recharging
    ///     back above them. Runtime state, not set from YAML.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<float> Triggered = new();
}

/// <summary>
///     A single charge warning step: the threshold and what to play when it is crossed.
/// </summary>
[DataDefinition]
public sealed partial class SiliconChargeWarningThreshold
{
    /// <summary>
    ///     Fraction of max charge (0-1) at or below which the warning fires.
    /// </summary>
    [DataField(required: true)]
    public float Percent;

    /// <summary>
    ///     Sound played to the Silicon's own player when this threshold is crossed.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    ///     Optional popup message shown to the Silicon's own player.
    /// </summary>
    [DataField]
    public LocId? PopUp;
}
