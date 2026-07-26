using Content.Shared._Crescent.Diplomacy;

namespace Content.Client._Crescent.Diplomacy;

public sealed class FactionStatusSystem : EntitySystem
{
    public event Action<AllFactionRelationsUpdatedEvent>? RelationsUpdated;
    public event Action<PlayerFactionUpdatedEvent>? PlayerFactionUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AllFactionRelationsUpdatedEvent>(OnRelationsUpdated);
        SubscribeNetworkEvent<PlayerFactionUpdatedEvent>(OnPlayerFactionUpdated);
    }

    private void OnRelationsUpdated(AllFactionRelationsUpdatedEvent ev)
    {
        RelationsUpdated?.Invoke(ev);
    }

    private void OnPlayerFactionUpdated(PlayerFactionUpdatedEvent ev)
    {
        PlayerFactionUpdated?.Invoke(ev);
    }
}
