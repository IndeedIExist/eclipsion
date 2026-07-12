using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.Cloning;

[RegisterComponent, NetworkedComponent]
public sealed partial class CloneOrganComponent : Component
{
    /// <summary>
    /// Client sprite layer shader prototype id applied to this organ.
    /// </summary>
    [DataField]
    public string SpriteShader = "CloneOrganOverlay";
}

