namespace Content.Server._Crescent.Bank;

/// <summary>
/// Server-side log of PDA bank transfers for this mob (current round).
/// </summary>
[RegisterComponent]
public sealed partial class BankTransferHistoryComponent : Component
{
    public const int MaxEntries = 50;

    public List<BankTransferHistoryRecord> Entries = new();
}

public sealed class BankTransferHistoryRecord
{
    public bool Outgoing;
    public string CounterpartyName = string.Empty;
    public int Amount;
    public string Comment = string.Empty;
    public TimeSpan RoundTimestamp;
}
