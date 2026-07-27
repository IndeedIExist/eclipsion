using Content.Server.Engineering.Components;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private readonly SharedMapSystem _mapSystemCompat = default!;
        [Dependency] private readonly TurfSystem _turf = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
        }

        private async void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;
            var gridUid = args.ClickLocation.GetGridUid(EntityManager);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;
            if (!_mapSystemCompat.TryGetTileRef(gridUid.Value, grid, args.ClickLocation, out var tileRef))
                return;

            bool IsTileClear()
            {
                return tileRef.Tile.IsEmpty == false && !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
            }

            if (!IsTileClear())
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new AwaitedDoAfterEvent(), null)
                {
                    BreakOnMove = true,
                };
                var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);

                if (result != DoAfterStatus.Finished)
                    return;
            }

            if (component.Deleted || !IsTileClear())
                return;

            if (EntityManager.TryGetComponent(uid, out StackComponent? stackComp)
                && component.RemoveOnInteract && !_stackSystem.Use(uid, 1, stackComp))
            {
                return;
            }

            EntityManager.SpawnEntity(component.Prototype, args.ClickLocation.SnapToGrid(grid));

            if (component.RemoveOnInteract && stackComp == null && !((!EntityManager.EntityExists(uid) ? EntityLifeStage.Deleted : EntityManager.GetComponent<MetaDataComponent>(component.Owner).EntityLifeStage) >= EntityLifeStage.Deleted))
                EntityManager.DeleteEntity(uid);
        }
    }
}
