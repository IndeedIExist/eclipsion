using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.SpaceEvents;

[Serializable, NetSerializable]
public sealed class EmpZoneActivatedEvent : EntityEventArgs
{
    public int ZoneId;
	public Vector2 Center;
    public float Radius;

    public EmpZoneActivatedEvent(int zoneId, Vector2 center, float radius)
    {
		ZoneId = zoneId;
        Center = center;
        Radius = radius;
    }
}

[Serializable, NetSerializable]
public sealed class EmpZoneDeactivatedEvent : EntityEventArgs
{
    public int ZoneId;

    public EmpZoneDeactivatedEvent(int zoneId)
    {
        ZoneId = zoneId;
    }
}