using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.RoundEnd;

/// <summary>
/// Spawns the derelict Analiesse somewhere in the sector and hides the CMM directive's auth key aboard her.
/// Admin-run only: the prototype has no StationEvent component, so the scheduler cannot roll it midround.
/// </summary>
public sealed class AnaliesseEventRuleSystem : GameRuleSystem<AnaliesseEventRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Started(EntityUid uid, AnaliesseEventRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Load the hull on its own map first, then move it into the sector.
        var loadMapUid = _mapSystem.CreateMap(out var loadMap);
        if (!_mapLoader.TryLoadGrid(loadMap, new ResPath(component.GridPath), out var gridUids))
        {
            Log.Error($"Analiesse event could not load grid {component.GridPath}");
            return;
        }

        var gridUid = gridUids.Value.Owner;
        component.GridUid = gridUid;

        _shuttle.SetIFFColor(gridUid, Color.DarkGray);

        var mapUid = _mapSystem.GetMap(GameTicker.DefaultMap);
        var offset = _random.NextVector2Box(component.MinX, component.MinY, component.MaxX, component.MaxY);

        if (TryComp<ShuttleComponent>(gridUid, out var shuttle))
            _shuttle.FTLToCoordinates(gridUid, shuttle, new EntityCoordinates(mapUid, offset), 0f, 0f, 30f);
        else
            _transform.SetCoordinates(gridUid, new EntityCoordinates(mapUid, offset)); // no FTL drive — just park her

        PlaceKeys(gridUid, component);
    }

    /// <summary>Drops the key(s) on random real tiles of the wreck, so they are always somewhere inside her.</summary>
    private void PlaceKeys(EntityUid gridUid, AnaliesseEventRuleComponent component)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Log.Error("Analiesse event: spawned entity has no grid component, cannot place the key.");
            return;
        }

        var tiles = _mapSystem.GetAllTiles(gridUid, grid).ToList();
        if (tiles.Count == 0)
        {
            Log.Error("Analiesse event: wreck has no tiles, cannot place the key.");
            return;
        }

        for (var i = 0; i < component.KeyCount; i++)
        {
            var tile = _random.Pick(tiles);
            var coords = _mapSystem.GridTileToLocal(gridUid, grid, tile.GridIndices);
            Spawn(component.Key, coords);
        }
    }
}
