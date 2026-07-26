using System.Threading;

namespace Content.Server._Crescent.PlanetfallObjectives;

[RegisterComponent]
public sealed partial class PlanetfallBarrierAnnouncerComponent : Component
{
    [DataField]
    public float ReleaseDelay = 1800f;

    public bool SchedulesCreated;

    public bool Released;

    [NonSerialized]
    public CancellationTokenSource TimerCancel = new();
}
