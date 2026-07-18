using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Payment;

/// <summary>Shared payroll limits, enforced on both the console UI and the server-side payout.</summary>
public static class PaymentConsole
{
    /// <summary>
    /// Cap on a single member's hourly salary. Clamped server-side in the payroll system and mirrored
    /// on the console so the operator sees the limit rather than having a larger figure silently trimmed.
    /// </summary>
    public const int MaxSalaryPerHour = 10_000;
}

[Serializable, NetSerializable]
public enum PaymentConsoleUiKey : byte
{
    Key
}

/// <summary>
/// One row of the payroll list. <see cref="User"/> is the identity salaries are keyed by; it survives
/// respawns and reconnects, unlike the mob.
/// </summary>
[Serializable, NetSerializable]
public struct PaymentMemberEntry
{
    public NetUserId User;
    public string Name;
    public string Job;
    public int SalaryPerHour;

    /// <summary>False while the member can't be credited (disconnected or unattached); they accrue nothing.</summary>
    public bool Payable;

    /// <summary>True when the member is on the payroll but no longer in the faction.</summary>
    public bool Stale;
}

[Serializable, NetSerializable]
public sealed class PaymentConsoleState : BoundUserInterfaceState
{
    public int Treasury;
    public bool TreasuryBound;
    public List<PaymentMemberEntry> Members;

    public PaymentConsoleState(int treasury, bool treasuryBound, List<PaymentMemberEntry> members)
    {
        Treasury = treasury;
        TreasuryBound = treasuryBound;
        Members = members;
    }
}

[Serializable, NetSerializable]
public sealed class PaymentSetSalaryMessage : BoundUserInterfaceMessage
{
    public NetUserId User;
    public int SalaryPerHour;

    public PaymentSetSalaryMessage(NetUserId user, int salaryPerHour)
    {
        User = user;
        SalaryPerHour = salaryPerHour;
    }
}

[Serializable, NetSerializable]
public sealed class PaymentClearSalaryMessage : BoundUserInterfaceMessage
{
    public NetUserId User;

    public PaymentClearSalaryMessage(NetUserId user)
    {
        User = user;
    }
}

[Serializable, NetSerializable]
public sealed class PaymentBonusMessage : BoundUserInterfaceMessage
{
    public NetUserId User;
    public int Amount;
    public string Reason;

    public PaymentBonusMessage(NetUserId user, int amount, string reason)
    {
        User = user;
        Amount = amount;
        Reason = reason;
    }
}
