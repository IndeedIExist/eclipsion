using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Crescent.Diplomacy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Crescent.Diplomacy;

public sealed class FactionStatusUIController : UIController, IOnSystemChanged<FactionStatusSystem>
{
    [UISystemDependency] private readonly FactionStatusSystem? _system = default;

    private Dictionary<string, Dictionary<string, FactionRelation>> _allRelations = new();
    private string? _myFaction;

    private FactionStatusWidget? Widget => UIManager.GetActiveUIWidgetOrNull<FactionStatusWidget>();

    public override void Initialize()
    {
        base.Initialize();

        var loader = UIManager.GetUIController<GameplayStateLoadController>();
        loader.OnScreenLoad += OnScreenLoad;
        loader.OnScreenUnload += OnScreenUnload;
    }

    public void OnSystemLoaded(FactionStatusSystem system)
    {
        system.RelationsUpdated += OnRelationsUpdated;
        system.PlayerFactionUpdated += OnPlayerFactionUpdated;
    }

    public void OnSystemUnloaded(FactionStatusSystem system)
    {
        system.RelationsUpdated -= OnRelationsUpdated;
        system.PlayerFactionUpdated -= OnPlayerFactionUpdated;
    }

    private void OnScreenLoad()
    {
        RefreshWidget();
    }

    private void OnScreenUnload()
    {
        Widget?.Clear();
        _myFaction = null;
    }

    private void OnRelationsUpdated(AllFactionRelationsUpdatedEvent ev)
    {
        _allRelations = ev.AllRelations;
        RefreshWidget();
    }

    private void OnPlayerFactionUpdated(PlayerFactionUpdatedEvent ev)
    {
        _myFaction = ev.FactionId;
        RefreshWidget();
    }

    private void RefreshWidget()
    {
        var widget = Widget;
        if (widget == null)
            return;

        if (_myFaction == null || !_allRelations.TryGetValue(_myFaction, out var relations))
        {
            widget.Clear();
            return;
        }

        widget.UpdateRelations(_myFaction, relations);
    }
}
