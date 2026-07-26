using Content.Shared._Crescent.Cloning;
using Content.Shared.Body.Systems;

namespace Content.Server._Crescent.Cloning;

/// <summary>
/// Marks clone-grown organs after a clone body is spawned, decoupled from the cloning pod implementation.
/// </summary>
public sealed class CloneOrganMarkingSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, CloneBodySpawnedForOrganMarkingEvent>(OnCloneBodySpawned);
    }

    private void OnCloneBodySpawned(EntityUid uid, TransformComponent _, ref CloneBodySpawnedForOrganMarkingEvent args)
    {
        foreach (var (organUid, _) in _body.GetBodyOrgans(uid))
            EnsureComp<CloneOrganComponent>(organUid);
    }
}
