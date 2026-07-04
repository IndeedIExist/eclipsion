using Robust.Shared.Audio;
using Robust.Shared.Maths;

namespace Content.Server._Crescent.Spray;

/// <summary>
/// A spray can that paints a multi-stage graffiti onto a tile. Clicking the same tile repeatedly
/// advances it to the next stage decal and plays that stage's sound, in order, from start to finish.
/// The current stage is derived from the decal already on the tile, so progress is per-tile and
/// survives dropping/swapping the item.
/// </summary>
[RegisterComponent]
public sealed partial class GraffitiSprayComponent : Component
{
    /// <summary>Ordered decal prototype ids, one per stage (first click paints index 0).</summary>
    [DataField]
    public List<string> StageDecals = new();

    /// <summary>Sound played when advancing to each stage. Index-aligned with <see cref="StageDecals"/>.</summary>
    [DataField]
    public List<SoundSpecifier> StageSounds = new();

    /// <summary>Optional tint applied to the painted decal.</summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// Tile step applied per stage, relative to the first-clicked tile. Each successive click
    /// paints the next part this many tiles over. Default paints one tile to the left each time,
    /// so the full piece reads across a row.
    /// </summary>
    [DataField]
    public Vector2i StageOffset = new(-1, 0);

    /// <summary>Remaining uses, set from <see cref="Capacity"/> on init.</summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public int Charges;

    /// <summary>Total uses. <see cref="int.MaxValue"/> means infinite.</summary>
    [DataField]
    public int Capacity = int.MaxValue;
}
