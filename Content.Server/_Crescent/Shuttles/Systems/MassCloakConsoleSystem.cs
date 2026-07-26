using Content.Server._Crescent.Shuttles.Components;
using Content.Shared._Crescent.Shuttles.BUIStates;
using Content.Shared._Crescent.Shuttles.Events;
using Content.Shared.Construction.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Crescent.Shuttles.Systems;

[UsedImplicitly]
public sealed partial class MassCloakConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MassCloakConsoleComponent, AnchorAttemptEvent>(OnTryAnchor);
        SubscribeLocalEvent<MassCloakConsoleComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<MassCloakConsoleComponent, MassCloakSetMessage>(OnSetMassCloak);
        SubscribeLocalEvent<MassCloakConsoleComponent, MapInitEvent>(OnInit);
    }

    private void OnTryAnchor(Entity<MassCloakConsoleComponent> obj, ref AnchorAttemptEvent args)
    {
        var targetTransform = Transform(obj);
        if (targetTransform.GridUid is null || obj.Comp.originalGrid is null)
            return;
        if (targetTransform.GridUid == obj.Comp.originalGrid)
            return;
        args.Cancel();
    }

    private void OnInit(Entity<MassCloakConsoleComponent> obj, ref MapInitEvent args)
    {
        var targetTransform = Transform(obj);
        if (targetTransform.GridUid is not null)
            obj.Comp.originalGrid = targetTransform.GridUid;
    }

    private void OnSetMassCloak(EntityUid uid, MassCloakConsoleComponent component, MassCloakSetMessage args)
    {
        component.MassCloakEnabled = args.Enabled;
        component.MassCloakRange = Math.Clamp(args.Range, MassCloakConsoleComponent.MassCloakMinRange, MassCloakConsoleComponent.MassCloakMaxRange);
        Dirty(uid, component);
        UpdateInterface(uid, component);
    }

    private void OnAnchor(EntityUid uid, MassCloakConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        // If we anchor / re-anchor then make sure state is up to date.
        _uiSystem.SetUiState(uid, MassCloakConsoleUiKey.Key, new MassCloakConsoleBoundUserInterfaceState()
        {
            MassCloakEnabled = component.MassCloakEnabled,
            MassCloakRange = component.MassCloakRange,
            MassCloakMinRange = MassCloakConsoleComponent.MassCloakMinRange,
            MassCloakMaxRange = MassCloakConsoleComponent.MassCloakMaxRange,
        });
    }

    public void UpdateInterface(EntityUid console, MassCloakConsoleComponent comp)
    {
        _uiSystem.SetUiState(
            console,
            MassCloakConsoleUiKey.Key,
            new MassCloakConsoleBoundUserInterfaceState()
            {
                MassCloakEnabled = comp.MassCloakEnabled,
                MassCloakRange = comp.MassCloakRange,
                MassCloakMinRange = MassCloakConsoleComponent.MassCloakMinRange,
                MassCloakMaxRange = MassCloakConsoleComponent.MassCloakMaxRange,
            });
    }
}
