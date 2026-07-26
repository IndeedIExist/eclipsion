using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Reflect;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class DeflectMasteryComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BonusPerBlock = 0.025f;

    [DataField, AutoNetworkedField]
    public float BonusPerPerfectParry = 0.1f;

    [DataField, AutoNetworkedField]
    public float MaxBonus = 0.35f;

    [DataField, AutoNetworkedField]
    public float DecayRate = 0.03f;

    [DataField, AutoNetworkedField]
    public float DecayDelay = 4f;

    [AutoNetworkedField]
    public float CurrentBonus;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan LastStackTime;
}
