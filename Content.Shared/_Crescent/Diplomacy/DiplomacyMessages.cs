using Content.Shared._Crescent.HullrotFaction;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Diplomacy;

public enum FactionRelation
{
    Neutral,
    War,
    Peace,
    Alliance,
    Trade
}

[Serializable, NetSerializable]
public sealed class AllFactionRelationsUpdatedEvent : EntityEventArgs
{
    public Dictionary<string, Dictionary<string, FactionRelation>> AllRelations { get; }
    public Dictionary<string, List<PendingProposal>> AllPending { get; }

    public AllFactionRelationsUpdatedEvent(
        Dictionary<string, Dictionary<string, FactionRelation>> allRelations,
        Dictionary<string, List<PendingProposal>> allPending)
    {
        AllRelations = allRelations;
        AllPending = allPending;
    }
}

[Serializable, NetSerializable]
public sealed class PlayerFactionUpdatedEvent : EntityEventArgs
{
    public string FactionId { get; }

    public PlayerFactionUpdatedEvent(string factionId)
    {
        FactionId = factionId;
    }
}

[Serializable, NetSerializable]
public enum DiplomacyConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DiplomacyConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public Dictionary<string, FactionRelation> Relations { get; }
    public string CurrentFactionId { get; }
    public List<PendingProposal> PendingProposals { get; }

    public DiplomacyConsoleBoundUserInterfaceState(
        Dictionary<string, FactionRelation> relations,
        string currentFactionId,
        List<PendingProposal> pendingProposals)
    {
        Relations = relations;
        CurrentFactionId = currentFactionId;
        PendingProposals = pendingProposals;
    }
}

[Serializable, NetSerializable]
public enum PendingProposalType
{
    Peace,
    Alliance,
    Trade
}

[Serializable, NetSerializable]
public sealed class PendingProposal
{
    public string FromFactionId { get; set; } = string.Empty;
    public string ToFactionId { get; set; } = string.Empty;
    public PendingProposalType Type { get; set; }
}

[Serializable, NetSerializable]
public sealed class DiplomacyDeclareWarMessage : BoundUserInterfaceMessage
{
    public string TargetFactionId { get; }

    public DiplomacyDeclareWarMessage(string targetFactionId)
    {
        TargetFactionId = targetFactionId;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyProposePeaceMessage : BoundUserInterfaceMessage
{
    public string TargetFactionId { get; }

    public DiplomacyProposePeaceMessage(string targetFactionId)
    {
        TargetFactionId = targetFactionId;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyProposeAllianceMessage : BoundUserInterfaceMessage
{
    public string TargetFactionId { get; }

    public DiplomacyProposeAllianceMessage(string targetFactionId)
    {
        TargetFactionId = targetFactionId;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyProposeTradeMessage : BoundUserInterfaceMessage
{
    public string TargetFactionId { get; }

    public DiplomacyProposeTradeMessage(string targetFactionId)
    {
        TargetFactionId = targetFactionId;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyBreakTradeMessage : BoundUserInterfaceMessage
{
    public string TargetFactionId { get; }

    public DiplomacyBreakTradeMessage(string targetFactionId)
    {
        TargetFactionId = targetFactionId;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyAcceptProposalMessage : BoundUserInterfaceMessage
{
    public string FromFactionId { get; }
    public PendingProposalType Type { get; }

    public DiplomacyAcceptProposalMessage(string fromFactionId, PendingProposalType type)
    {
        FromFactionId = fromFactionId;
        Type = type;
    }
}

[Serializable, NetSerializable]
public sealed class DiplomacyRejectProposalMessage : BoundUserInterfaceMessage
{
    public string FromFactionId { get; }
    public PendingProposalType Type { get; }

    public DiplomacyRejectProposalMessage(string fromFactionId, PendingProposalType type)
    {
        FromFactionId = fromFactionId;
        Type = type;
    }
}
