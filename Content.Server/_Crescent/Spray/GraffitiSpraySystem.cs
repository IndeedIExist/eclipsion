using System.Numerics;
using Content.Server.Decals;
using Content.Server.Popups;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._Crescent.Spray;

/// <summary>
/// Handles the <see cref="GraffitiSprayComponent"/> spray can: each click on a tile paints the next
/// stage of a graffiti and plays that stage's sound. Modeled on the crayon
/// (<see cref="CrayonSystem"/>) for interaction/sound and on DirectionalTilingSystem for the
/// tile-snap + decal lookup.
/// </summary>
public sealed class GraffitiSpraySystem : EntitySystem
{
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GraffitiSprayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GraffitiSprayComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnInit(EntityUid uid, GraffitiSprayComponent comp, ComponentInit args)
    {
        comp.Charges = comp.Capacity;
    }

    private void OnAfterInteract(EntityUid uid, GraffitiSprayComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || comp.StageDecals.Count == 0)
            return;

        if (!args.ClickLocation.IsValid(EntityManager))
        {
            args.Handled = true;
            return;
        }

        var gridUid = args.ClickLocation.EntityId;
        if (!HasComp<MapGridComponent>(gridUid))
            return; // clicked space or a non-grid parent

        // Snap the click to the tile centre so every click on the same origin tile resolves to the
        // same coordinate (rounding taken from DirectionalTilingSystem / DecalPlacementSystem).
        var click = args.ClickLocation;
        var originCentre = new Vector2(
            MathF.Round(click.X - 0.5f, MidpointRounding.AwayFromZero) + 0.5f,
            MathF.Round(click.Y - 0.5f, MidpointRounding.AwayFromZero) + 0.5f);

        var step = comp.StageOffset;

        // March out from the clicked tile in the step direction and count how many parts are
        // already painted, so the next part lands one tile further along.
        var nextIndex = 0;
        while (nextIndex < comp.StageDecals.Count
               && IsStagePainted(gridUid, StageCentre(originCentre, step, nextIndex), comp))
        {
            nextIndex++;
        }

        // Whole piece already painted - nothing to do (and no charge spent).
        if (nextIndex >= comp.StageDecals.Count)
        {
            args.Handled = true;
            return;
        }

        if (comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("crayon-interact-not-enough-left-text"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Paint the next part on its own tile, aligned like the crayon (offset by -0.5,-0.5).
        var partCentre = StageCentre(originCentre, step, nextIndex);
        var placeCoords = new EntityCoordinates(gridUid, partCentre).Offset(new Vector2(-0.5f, -0.5f));
        if (!_decals.TryAddDecal(comp.StageDecals[nextIndex], placeCoords, out _, comp.Color, cleanable: true))
            return;

        if (nextIndex < comp.StageSounds.Count)
            _audio.PlayPvs(comp.StageSounds[nextIndex], new EntityCoordinates(gridUid, partCentre));

        if (comp.Charges != int.MaxValue)
            comp.Charges--;

        args.Handled = true;
    }

    private static Vector2 StageCentre(Vector2 originCentre, Vector2 step, int index)
    {
        return originCentre + step * index;
    }

    /// <summary>Whether one of our stage parts is painted exactly on the given tile.</summary>
    private bool IsStagePainted(EntityUid gridUid, Vector2 tileCentre, GraffitiSprayComponent comp)
    {
        var expected = tileCentre + new Vector2(-0.5f, -0.5f);
        foreach (var (_, decal) in _decals.GetDecalsInRange(gridUid, tileCentre, 0.9f))
        {
            if (comp.StageDecals.Contains(decal.Id)
                && (decal.Coordinates - expected).LengthSquared() < 0.01f)
            {
                return true;
            }
        }

        return false;
    }
}
