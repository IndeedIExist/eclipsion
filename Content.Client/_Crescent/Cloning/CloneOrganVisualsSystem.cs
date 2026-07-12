using Content.Shared._Crescent.Cloning;
using Robust.Client.GameObjects;

namespace Content.Client._Crescent.Cloning;

public sealed class CloneOrganVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CloneOrganComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CloneOrganComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CloneOrganComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, CloneOrganComponent component, ComponentStartup args)
    {
        ApplyShader(uid, component);
    }

    private void OnShutdown(EntityUid uid, CloneOrganComponent component, ComponentShutdown args)
    {
        if (Terminating(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var idx = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(idx, null, null);
            idx++;
        }
    }

    private void OnAppearanceChange(EntityUid uid, CloneOrganComponent component, ref AppearanceChangeEvent args)
    {
        ApplyShader(uid, component, args.Sprite);
    }

    private void ApplyShader(EntityUid uid, CloneOrganComponent? comp = null, SpriteComponent? sprite = null)
    {
        if (comp == null && !TryComp(uid, out comp))
            return;

        if (!Resolve(uid, ref sprite, false))
            return;

        var idx = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(idx, comp.SpriteShader);
            idx++;
        }
    }
}
