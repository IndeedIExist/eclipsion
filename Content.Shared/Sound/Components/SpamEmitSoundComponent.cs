using Robust.Shared.GameStates;

namespace Content.Shared.Sound.Components;

/// <summary>
/// Repeatedly plays a sound with a randomized delay.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SpamEmitSoundComponent : BaseEmitSoundComponent
{
    /// <summary>
    /// The time at which the next sound will play.
    /// </summary>
    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan NextSound;

    /// <summary>
    /// The minimum time in seconds between playing the sound.
    /// </summary>
    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The maximum time in seconds between playing the sound.
    /// </summary>
    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(2);

    // Always Pvs.
    /// <summary>
    /// Content of a popup message to display whenever the sound plays.
    /// </summary>
    [DataField]
    public LocId? PopUp;

    /// <summary>
    /// If true, the sound and popup are played only to this entity's own player (if any),
    /// instead of to everyone in PVS range. Useful for personal alarms (e.g. Silicon low power)
    /// that should not annoy everyone nearby.
    /// </summary>
    [DataField]
    public bool PlayToOwnerOnly;

    /// <summary>
    /// Whether the timer is currently running and sounds are being played.
    /// Do not set this directly, use <see cref="EmitSoundSystem.SetEnabled"/>
    /// </summary>
    [DataField, AutoNetworkedField]
    [Access(typeof(SharedEmitSoundSystem))]
    public bool Enabled = true;
}
