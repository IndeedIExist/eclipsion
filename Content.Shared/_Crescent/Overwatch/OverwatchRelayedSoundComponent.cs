namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Relays sounds while watching through an Overwatch camera.
/// </summary>
[RegisterComponent]
public sealed partial class RatOverwatchRelayedSoundComponent : Component
{
    /// <summary>
    /// The relayed sound entity.
    /// </summary>
    public EntityUid? Relay;
}
