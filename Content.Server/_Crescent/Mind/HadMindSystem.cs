using Content.Shared._Crescent.Mind;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server._Crescent.Mind;

/// <summary>
///     Tracks when an entity has had a mind (was player-controlled) by adding <see cref="HadMindComponent"/>.
///     This component persists even after the mind is removed, allowing systems to check if an entity
///     was once controlled by a player.
/// </summary>
public sealed class HadMindSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartDroppedEvent>(OnBodyPartDropped);
    }

    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        // When a mind is added to an entity, mark it as having had a mind.
        EnsureComp<HadMindComponent>(ent);
    }

    /// <summary>
    ///     When a body part is detached from a body that had a mind,
    ///     copy the HadMindComponent to the detached part.
    /// </summary>
    private void OnBodyPartDropped(EntityUid uid, BodyComponent comp, ref BodyPartDroppedEvent args)
    {
        // uid is the body entity
        // args.Part is the detached body part
        if (HasComp<HadMindComponent>(uid))
        {
            EnsureComp<HadMindComponent>(args.Part);
        }
    }
}
