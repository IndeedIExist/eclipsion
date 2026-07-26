namespace Content.Shared._Crescent.Payment;

/// <summary>
/// A console that sets standing salaries for a faction's members, paid from that faction's treasury.
/// </summary>
/// <remarks>
/// Only server systems read this; it lives in Shared so prototypes can load it. Deliberately not
/// networked — everything the client renders arrives in <see cref="PaymentConsoleState"/>.
/// </remarks>
[RegisterComponent]
public sealed partial class PaymentConsoleComponent : Component
{
    /// <summary>
    /// Which faction's payroll and treasury this console administers. Set per prototype variant, the
    /// same way <c>OverwatchConsoleComponent.Faction</c> is.
    /// </summary>
    [DataField]
    public string Faction = string.Empty;
}
