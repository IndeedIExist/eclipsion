using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.DnaDatabase;

[Serializable, NetSerializable]
public sealed class DnaDatabaseToggleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DnaDatabaseBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Enabled;
    public string FactionId;
    public string FactionName;
    public int TotalJobs;
    public int AvailableJobs;
    public int UnlimitedJobs;

    public DnaDatabaseBoundUserInterfaceState(
        bool enabled,
        string factionId,
        string factionName,
        int totalJobs,
        int availableJobs,
        int unlimitedJobs)
    {
        Enabled = enabled;
        FactionId = factionId;
        FactionName = factionName;
        TotalJobs = totalJobs;
        AvailableJobs = availableJobs;
        UnlimitedJobs = unlimitedJobs;
    }
}

[Serializable, NetSerializable]
public enum DnaDatabaseUiKey : byte
{
    Key
}
