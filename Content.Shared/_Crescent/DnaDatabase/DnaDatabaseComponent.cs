using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.DnaDatabase;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaDatabaseComponent : Component
{
    [DataField]
    public string FactionId = "";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public EntityUid BoundGrid = EntityUid.Invalid;

    [DataField]
    public EntityUid BoundStation = EntityUid.Invalid;

    [DataField]
    public Dictionary<string, uint?> SavedJobSlots = new();
}
