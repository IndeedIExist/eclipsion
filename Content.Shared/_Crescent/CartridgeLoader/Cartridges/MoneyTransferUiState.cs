using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class MoneyTransferRecipientState
{
    public NetEntity Entity;
    public string Name;
    public string Job;

    public MoneyTransferRecipientState(NetEntity entity, string name, string job)
    {
        Entity = entity;
        Name = name;
        Job = job;
    }
}

[Serializable, NetSerializable]
public sealed class MoneyTransferHistoryEntryState
{
    public bool Outgoing;
    public string Counterparty;
    public int Amount;
    public string Comment;
    public string TimeText;

    public MoneyTransferHistoryEntryState(bool outgoing, string counterparty, int amount, string comment, string timeText)
    {
        Outgoing = outgoing;
        Counterparty = counterparty;
        Amount = amount;
        Comment = comment;
        TimeText = timeText;
    }
}

[Serializable, NetSerializable]
public sealed class MoneyTransferUiState : BoundUserInterfaceState
{
    public long Balance;
    public List<MoneyTransferRecipientState> Recipients;
    public List<MoneyTransferHistoryEntryState> History;
    public string? Error;
    /// <summary>Shown once after a successful outgoing transfer (green toast on client).</summary>
    public string? Success;

    public MoneyTransferUiState(
        long balance,
        List<MoneyTransferRecipientState> recipients,
        List<MoneyTransferHistoryEntryState> history,
        string? error,
        string? success = null)
    {
        Balance = balance;
        Recipients = recipients;
        History = history;
        Error = error;
        Success = success;
    }
}

[Serializable, NetSerializable]
public sealed class MoneyTransferUiMessageEvent : CartridgeMessageEvent
{
    public NetEntity Recipient;
    public int Amount;
    public string Comment;

    public MoneyTransferUiMessageEvent(NetEntity recipient, int amount, string comment)
    {
        Recipient = recipient;
        Amount = amount;
        Comment = comment;
    }
}
