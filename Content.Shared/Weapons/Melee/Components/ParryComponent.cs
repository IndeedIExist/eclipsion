using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Melee.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ParryComponent : Component
{
    // --- Timing ---

    [DataField, AutoNetworkedField]
    public float ParryWindowSeconds = 0.5f;

    [DataField, AutoNetworkedField]
    public float PerfectParryWindowSeconds = 0.2f;

    [DataField, AutoNetworkedField]
    public float ParryCooldownSeconds = 0.8f;

    [DataField, AutoNetworkedField]
    public float FailedParryStaminaDamage = 4f;

    [DataField, AutoNetworkedField]
    public float BlockStaminaDamage = 2f;

    [DataField, AutoNetworkedField]
    public float PerfectParryStaminaRestore = 15f;

    // --- Riposte ---

    [DataField, AutoNetworkedField]
    public float RiposteWindowSeconds = 1.2f;

    [DataField, AutoNetworkedField]
    public float RiposteDamageMultiplier = 2.5f;

    // --- State ---

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextParryTime;

    [AutoNetworkedField]
    public bool IsParrying;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan ParryStartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan ParryEndTime;

    [AutoNetworkedField]
    public bool CanRiposte;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan RiposteEndTime;

    [AutoNetworkedField]
    public bool ParrySucceeded;

    public EntityUid? ParrySoundEntity;

    // --- Sounds ---

    [DataField]
    public SoundSpecifier SoundEmptyParry = new SoundCollectionSpecifier("SwordEmptyParry")
    {
        Params = AudioParams.Default.WithVolume(2f),
    };

    [DataField]
    public SoundSpecifier SoundBlock = new SoundCollectionSpecifier("SwordBlock")
    {
        Params = AudioParams.Default.WithVolume(2f),
    };

    [DataField]
    public SoundSpecifier SoundPerfectParry = new SoundPathSpecifier("/Audio/Weapons/Melee/Sword/parry_perfect.ogg")
    {
        Params = AudioParams.Default.WithVolume(3f),
    };

    [DataField]
    public SoundSpecifier SoundRiposte = new SoundPathSpecifier("/Audio/Weapons/Melee/Sword/riposte.ogg")
    {
        Params = AudioParams.Default.WithVolume(4f),
    };

    [DataField]
    public SoundSpecifier SoundParryFail = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f),
    };
}
