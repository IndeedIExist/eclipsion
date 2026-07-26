using Robust.Shared.Audio;
using Content.Server.Sound.Components;
using Content.Shared.Sound.Components;

namespace Content.Server.Silicon;

/// <summary>
///     Applies a <see cref="SpamEmitSoundComponent"/> to a Silicon when its battery is drained, and removes it when it's not.
/// </summary>
[RegisterComponent]
public sealed partial class SiliconEmitSoundOnDrainedComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = default!;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(15);

    [DataField]
    public float PlayChance = 1f;

    [DataField]
    public string? PopUp;

    /// <summary>
    ///     If true, the drained alarm is played only to the Silicon's own player,
    ///     instead of to everyone in PVS range.
    /// </summary>
    [DataField]
    public bool PlayToOwnerOnly;
}
