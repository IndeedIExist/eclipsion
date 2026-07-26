using Robust.Shared.Prototypes;

namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// Drops the derelict Analiesse into the sector with the CMM directive's auth key aboard her.
///
/// This rule deliberately carries no StationEvent component in its prototype, so the random event scheduler can
/// never pick it — it only runs when an admin starts it by hand. Until then the CMM directive is uncompletable,
/// which is the intent: the wreck is a storyteller beat, not a midround roll.
/// </summary>
[RegisterComponent, Access(typeof(AnaliesseEventRuleSystem))]
public sealed partial class AnaliesseEventRuleComponent : Component
{
    /// <summary>Hull used for the wreck. Any ship map works; swap it freely.</summary>
    [DataField]
    public string GridPath = "/Maps/_Crescent/Shuttles/CMM/provost.yml";

    /// <summary>The objective item hidden aboard her.</summary>
    [DataField]
    public EntProtoId Key = "AnaliesseAuthKey";

    /// <summary>How many copies of the key to place. More than one makes the race less winner-take-all.</summary>
    [DataField]
    public int KeyCount = 1;

    /// <summary>IFF label the wreck shows up as on scanners.</summary>
    [DataField]
    public string IffName = "Analiesse";

    /// <summary>Box (relative to the default map origin) the wreck is dropped somewhere inside.</summary>
    [DataField]
    public float MinX = -2000f;

    [DataField]
    public float MinY = -2000f;

    [DataField]
    public float MaxX = 2000f;

    [DataField]
    public float MaxY = 2000f;

    /// <summary>The spawned wreck, once it exists.</summary>
    [ViewVariables]
    public EntityUid? GridUid;
}
