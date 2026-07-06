using System;
using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Crescent.Overlays;

/// <summary>
/// Darkens the area behind the player using a procedural cone shader.
/// The transition has a soft ±15° edge and a distance fade for a gentle look.
///
/// Unlike the previous version, this overlay does NOT request the screen texture
/// and the shader never reads the framebuffer. It only alpha-blends translucent
/// black over the rear cone, which removes the sRGB/backend-dependent render
/// errors that crashed many clients on both Linux and Windows.
/// </summary>
public sealed class BackFovOverlay : Overlay
{
    [Dependency] private readonly IPlayerManager    _playerManager = default!;
    [Dependency] private readonly IEntityManager    _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager  = default!;
    [Dependency] private readonly IInputManager     _input         = default!;
    [Dependency] private readonly IEyeManager       _eye           = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    // 140° half-FOV → 280° visible, 80° back zone
    internal const  double HalfFovRad = 140.0 * Math.PI / 180.0;
    internal static readonly float HalfFovCos = (float) Math.Cos(HalfFovRad);

    private readonly ShaderInstance        _shader;
    private readonly SharedTransformSystem _xformSys;

    public BackFovOverlay()
    {
        IoCManager.InjectDependencies(this);
        _xformSys = _entityManager.System<SharedTransformSystem>();
        _shader   = _protoManager.Index<ShaderPrototype>("BackFov").InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (!_entityManager.TryGetComponent(player, out EyeComponent? eye))
            return false;
        if (args.Viewport.Eye != eye.Eye)
            return false;
        // Ghosts have 360° vision — skip the back-FOV darkening.
        if (_entityManager.HasComponent<GhostComponent>(player))
            return false;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
            return;

        if (!_entityManager.TryGetComponent<TransformComponent>(player.Value, out var xform))
            return;

        if (xform.MapID != args.MapId)
            return;

        var worldPos = _xformSys.GetWorldPosition(xform);
        var worldRot = _xformSys.GetWorldRotation(xform).Theta;
        // facingDir: same Y convention as FRAGCOORD (Y+ = up), no flip needed
        var facingDir = new Angle(worldRot).ToWorldVec();
        // In combat mode, override with smooth mouse direction for 360° FOV tracking.
        if (_entityManager.TryGetComponent<CombatModeComponent>(player.Value, out var combatComp) && combatComp.IsInCombatMode)
        {
            var mouseScreen = _input.MouseScreenPosition;
            if (mouseScreen.IsValid)
            {
                var mouseMap = _eye.PixelToMap(mouseScreen);
                if (mouseMap.MapId == xform.MapID)
                {
                    var toMouse = mouseMap.Position - worldPos;
                    var lenSq = toMouse.LengthSquared();
                    if (lenSq > 0.01f)
                        facingDir = toMouse / MathF.Sqrt(lenSq);
                }
            }
        }

        // WorldToLocal: Y=0 at top (screen-down). FRAGCOORD: Y=0 at bottom (OpenGL). Flip Y.
        var viewportLocal  = args.Viewport.WorldToLocal(worldPos);
        var playerPixelPos = new Vector2(viewportLocal.X, args.ViewportBounds.Height - viewportLocal.Y);

        _shader.SetParameter("playerPixelPos", playerPixelPos);
        _shader.SetParameter("facingDir",       facingDir);
        _shader.SetParameter("halfFovCos",      HalfFovCos);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}

/// <summary>
/// Registers the back-FOV overlay for the local client. Intentionally minimal:
/// it no longer iterates entities or mutates any <see cref="SpriteComponent"/>
/// state, so sprites can never get stuck invisible/translucent and there is no
/// per-frame cost. The darkening is handled entirely by <see cref="BackFovOverlay"/>.
/// </summary>
public sealed class BackFovSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new BackFovOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<BackFovOverlay>();
    }
}
