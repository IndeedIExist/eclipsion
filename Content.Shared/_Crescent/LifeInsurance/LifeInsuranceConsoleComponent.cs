using Content.Shared.Materials;
using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.LifeInsurance;

[RegisterComponent, NetworkedComponent]
public sealed partial class LifeInsuranceConsoleComponent : Component
{
    [DataField]
    public int RequiredProteins = 9000;

    [DataField]
    public int BaseRequiredCredits = 5000;

    [DataField]
    public string ProteinMaterialId = "ExoticProteins";

    [DataField]
    public float MaxTargetDistance = 400f;
}
