using System.Numerics;
using Content.Client.Resources;
using Content.Shared.Execution;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Execution;

/// <summary>
/// Draws a pulsing red "finishing off" marker (a downward chevron plus a label) above every entity
/// that currently has an <see cref="ExecutionTargetComponent"/>, i.e. someone who is being executed
/// right now.
/// </summary>
public sealed class ExecutionMarkerOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ExecutionMarkerOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var currentMap = _eye.CurrentMap;

        // Pulse the alpha a few times a second so it reads as an urgent, live threat.
        var t = (float) _timing.RealTime.TotalSeconds;
        var pulse = 0.65f + 0.35f * MathF.Sin(t * 9f);

        var query = _entManager.AllEntityQueryEnumerator<ExecutionTargetComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (xform.MapID != currentMap)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);

            // Anchor above the victim's head.
            var screenPos = _eye.WorldToScreen(worldPos + new Vector2(0f, 0.9f));

            var red = Color.Red.WithAlpha(pulse);
            var shadow = Color.Black.WithAlpha(pulse * 0.85f);

            // Downward chevron pointing at the head.
            DrawChevron(handle, screenPos + new Vector2(0f, 14f), red, shadow);

            // Centered label above the chevron.
            var text = Loc.GetString(marker.Text);
            var width = MeasureWidth(text);
            var textPos = screenPos - new Vector2(width / 2f, 0f);

            handle.DrawString(_font, textPos + new Vector2(1f, 1f), text, shadow);
            handle.DrawString(_font, textPos, text, red);
        }
    }

    private static void DrawChevron(DrawingHandleScreen handle, Vector2 tip, Color color, Color shadow)
    {
        // A small filled "▼" made of two triangles, with a subtle shadow underneath.
        const float halfWidth = 7f;
        const float height = 9f;

        var left = tip + new Vector2(-halfWidth, -height);
        var right = tip + new Vector2(halfWidth, -height);

        Span<Vector2> shadowTri =
        [
            left + new Vector2(1f, 1f),
            right + new Vector2(1f, 1f),
            tip + new Vector2(1f, 1f),
        ];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, shadowTri, shadow);

        Span<Vector2> tri = [left, right, tip];
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tri, color);
    }

    private float MeasureWidth(string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }
}
