using Robust.Shared.Audio;

namespace Content.Server._Crescent.Taxation;

/// <summary>
/// A per-faction treasury console. Displays and dispenses the tax revenue accumulated in the
/// owning station's <see cref="Content.Server.Crescent.Dispenser.StationTradeMarketComponent"/>.
/// Access is gated by the console's <c>AccessReader</c> (faction funds access). If someone
/// without access opens it, a security breach is raised: an alarm sounds and, after
/// <see cref="IntrusionDelay"/>, the treasury is repeatedly looted until an authorized
/// member re-secures the console.
/// </summary>
[RegisterComponent]
public sealed partial class FactionTreasuryConsoleComponent : Component
{
    /// <summary>
    /// How long after an unauthorized access before the first theft fires.
    /// </summary>
    [DataField]
    public TimeSpan IntrusionDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Credits stolen from the treasury each time the theft fires (clamped to available balance).
    /// The stolen credits are spawned as physical cash at the console.
    /// </summary>
    [DataField]
    public int TheftAmount = 5000;

    /// <summary>
    /// Sound played when a security breach begins and each time the treasury is looted.
    /// </summary>
    [DataField]
    public SoundSpecifier AlarmSound =
        new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    // --- Runtime state (not authored in YAML) -----------------------------

    /// <summary>Whether a security breach is currently active.</summary>
    [ViewVariables]
    public bool AlarmActive;

    /// <summary>Server time at which the next theft fires, if a breach is active.</summary>
    [ViewVariables]
    public TimeSpan? NextTheft;
}
