using System.Numerics;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Server._Crescent.SpaceEvents.Components;

[RegisterComponent, Access(typeof(AnomalyStationRule))]
public sealed partial class AnomalyStationRuleComponent : Component
{
    [DataField]
    public float MinCoord = -3000f;

    [DataField]
    public float MaxCoord = 3000f;

    [DataField(required: true)]
    public string StationMapPath;

    [DataField(required: true)]
    public LocId Announcement;

    [DataField("artifactSpawnerPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ArtifactSpawnerPrototype = "RandomArtifactSpawner";

    public Vector2 Coordinates;
}