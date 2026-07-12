namespace Content.Shared._Crescent.Overwatch;

/// <summary>
/// Компонент для ретрансляции звуков при наблюдении через камеру Overwatch.
/// </summary>
[RegisterComponent]
public sealed partial class RatOverwatchRelayedSoundComponent : Component
{
    /// <summary>
    /// Сущность ретранслируемого звука.
    /// </summary>
    public EntityUid? Relay;
}
