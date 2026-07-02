using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

[Serializable, NetSerializable]
public sealed class ParryAttemptEvent : EntityEventArgs
{
    public readonly NetEntity Weapon;

    public ParryAttemptEvent(NetEntity weapon)
    {
        Weapon = weapon;
    }
}

[Serializable, NetSerializable]
public sealed class ParryVisualEvent : EntityEventArgs
{
    public NetEntity Parrier;
    public bool Success;

    public ParryVisualEvent(NetEntity parrier, bool success)
    {
        Parrier = parrier;
        Success = success;
    }
}

/// <summary>
/// Sent to all nearby clients when a perfect parry opens a riposte window, so the parrier's
/// client can show the opportunity (glow indicator, screen kick) even though the parry itself
/// was resolved on the attacker's predicted swing, not the parrier's own input.
/// </summary>
[Serializable, NetSerializable]
public sealed class RiposteWindowOpenEvent : EntityEventArgs
{
    public NetEntity Parrier;
    public float Duration;

    public RiposteWindowOpenEvent(NetEntity parrier, float duration)
    {
        Parrier = parrier;
        Duration = duration;
    }
}

[Serializable, NetSerializable]
public sealed class RiposteVisualEvent : EntityEventArgs
{
    public NetEntity Attacker;
    public NetEntity Weapon;

    public RiposteVisualEvent(NetEntity attacker, NetEntity weapon)
    {
        Attacker = attacker;
        Weapon = weapon;
    }
}

public sealed class ParrySuccessEvent : EntityEventArgs
{
    public EntityUid Attacker;
    public EntityUid Parrier;
    public EntityUid AttackerWeapon;
    public bool IsPerfect;

    public ParrySuccessEvent(EntityUid attacker, EntityUid parrier, EntityUid attackerWeapon, bool isPerfect)
    {
        Attacker = attacker;
        Parrier = parrier;
        AttackerWeapon = attackerWeapon;
        IsPerfect = isPerfect;
    }
}
