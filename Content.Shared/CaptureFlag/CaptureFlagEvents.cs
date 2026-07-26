using Robust.Shared.Serialization;

namespace Content.Shared.CaptureFlag;

[Serializable, NetSerializable]
public sealed class CaptureFlagWonEvent : EntityEventArgs
{
    public string Team { get; }
    public CaptureFlagWonEvent(string team)
    {
        Team = team;
    }
}

