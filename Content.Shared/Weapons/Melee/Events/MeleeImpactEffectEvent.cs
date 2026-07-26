using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

/// <summary>
/// Networked companion to the melee hit red-flash effect. Tells the victim's and bystanders' clients to
/// play the hit-punch sprite animation (and, for whichever local player is actually one of the targets,
/// a small camera shake). The attacker gets both of these instantly via client-side prediction instead,
/// so the server only sends this to everyone else - see the Filter used alongside the color flash effect.
/// </summary>
[Serializable, NetSerializable]
public sealed class MeleeImpactEffectEvent : EntityEventArgs
{
    public List<NetEntity> Targets;

    public MeleeImpactEffectEvent(List<NetEntity> targets)
    {
        Targets = targets;
    }
}
