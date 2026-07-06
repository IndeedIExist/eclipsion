using Robust.Client.Graphics;

namespace Content.Client.Execution;

/// <summary>
/// Registers the <see cref="ExecutionMarkerOverlay"/> so the red finish-off indicator is drawn above
/// entities that are currently being executed.
/// </summary>
public sealed class ExecutionMarkerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<ExecutionMarkerOverlay>())
            _overlay.AddOverlay(new ExecutionMarkerOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<ExecutionMarkerOverlay>();
    }
}
