using Robust.Shared.Localization;

namespace Content.Shared._Crescent.Dispenser;

/// <summary>
/// If an entity used on this dispenser has any of the listed components, the interaction is denied.
/// </summary>
[RegisterComponent]
public sealed partial class DispenserRejectedComponentsComponent : Component
{
    /// <summary>
    /// Component names as registered in <see cref="Robust.Shared.GameObjects.ComponentFactory"/>.
    /// </summary>
    [DataField]
    public List<string> RejectedComponents = new();

    [DataField]
    public LocId DenyPopup = string.Empty;
}
