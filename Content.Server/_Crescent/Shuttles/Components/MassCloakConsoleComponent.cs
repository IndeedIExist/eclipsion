using Content.Server.Shuttles.Systems;
using Content.Server._Crescent.Shuttles.Systems;
using Content.Shared._Crescent.Shuttles.Components;
using Robust.Shared.GameStates;

namespace Content.Server._Crescent.Shuttles.Components;

[RegisterComponent, Access(typeof(MassCloakConsoleSystem), typeof(ShuttleSystem))]
public sealed partial class MassCloakConsoleComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("massCloakEnabled")]
    public bool MassCloakEnabled = false;

    [ViewVariables(VVAccess.ReadWrite), DataField("massCloakRange")]
    public float MassCloakRange = 20f;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? originalGrid = null;

    /// <summary>
    /// List of grids currently being cloaked by this console
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> CloakedGrids = new();

    public const float MassCloakMinRange = 20f;
    public const float MassCloakMaxRange = 500f;
}
