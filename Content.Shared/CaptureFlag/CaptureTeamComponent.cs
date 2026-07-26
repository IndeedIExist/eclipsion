using Robust.Shared.GameStates;

namespace Content.Shared.CaptureFlag;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CaptureTeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Team = string.Empty;
}

