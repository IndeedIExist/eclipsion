using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Melee;

public sealed class SharedParrySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private static readonly SoundPathSpecifier FinisherSound = new("/Audio/Weapons/Melee/Sword/kill-sound.ogg")
    {
        Params = AudioParams.Default.WithVolume(5f),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<ParryAttemptEvent>(OnParryAttempt);
    }

    private void OnParryAttempt(ParryAttemptEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryComp<ParryComponent>(user, out var parry))
            return;

        if (!_combatMode.IsInCombatMode(user))
            return;

        if (!_melee.TryGetWeapon(user, out var weaponUid, out _) || weaponUid == user)
            return;

        if (HasComp<GunComponent>(weaponUid))
            return;

        var curTime = _timing.CurTime;

        if (parry.IsParrying || parry.NextParryTime > curTime)
            return;

        // Weapons can override the wielder's base parry timing to give themselves a distinct feel,
        // e.g. a katana rewards tight timing with a narrow perfect-parry window, while a claymore is
        // more forgiving but less precise.
        var parryWindow = parry.ParryWindowSeconds;
        if (TryComp<MeleeWeaponComponent>(weaponUid, out var weaponForParry))
            parryWindow = weaponForParry.ParryWindowOverride ?? parryWindow;

        parry.IsParrying = true;
        parry.ParrySucceeded = false;
        parry.ParryStartTime = curTime;
        parry.ParryEndTime = curTime + TimeSpan.FromSeconds(parryWindow);
        parry.NextParryTime = curTime + TimeSpan.FromSeconds(parry.ParryCooldownSeconds);
        Dirty(user, parry);

        if (_net.IsServer)
        {
            parry.ParrySoundEntity = _audio.PlayPvs(parry.SoundEmptyParry, user)?.Entity;
            RaiseNetworkEvent(new ParryVisualEvent(GetNetEntity(user), true));
        }
    }

    private bool IsFacingAttacker(EntityUid target, EntityUid attacker)
    {
        var targetPos = _transform.GetWorldPosition(target);
        var attackerPos = _transform.GetWorldPosition(attacker);
        var dirToAttacker = attackerPos - targetPos;

        if (dirToAttacker.LengthSquared() < 0.01f)
            return true;

        var targetRotation = _transform.GetWorldRotation(target);
        var facingDir = targetRotation.ToWorldVec();
        var dot = System.Numerics.Vector2.Dot(
            System.Numerics.Vector2.Normalize(facingDir),
            System.Numerics.Vector2.Normalize(dirToAttacker));

        return dot > 0.3f;
    }

    public bool TryParry(EntityUid target, EntityUid attacker, EntityUid attackerWeapon)
    {
        if (!TryComp<ParryComponent>(target, out var parry))
            return false;

        if (!parry.IsParrying)
            return false;

        // Prevent entities from parrying themselves (attacker and target being the same)
        if (attacker == target)
            return false;

        var curTime = _timing.CurTime;
        if (curTime > parry.ParryEndTime)
            return false;

        // Use the defender's own weapon to determine their perfect-parry window, if it overrides one.
        var perfectWindow = parry.PerfectParryWindowSeconds;
        if (_melee.TryGetWeapon(target, out var defenderWeaponUid, out _) &&
            TryComp<MeleeWeaponComponent>(defenderWeaponUid, out var defenderWeapon))
        {
            perfectWindow = defenderWeapon.PerfectParryWindowOverride ?? perfectWindow;
        }

        var timeSinceParry = (curTime - parry.ParryStartTime).TotalSeconds;
        var isFacing = IsFacingAttacker(target, attacker);
        var isPerfect = isFacing && timeSinceParry <= perfectWindow;

        parry.IsParrying = false;
        parry.ParrySucceeded = true;

        if (isPerfect)
        {
            parry.CanRiposte = true;
            parry.RiposteEndTime = curTime + TimeSpan.FromSeconds(parry.RiposteWindowSeconds);
        }

        Dirty(target, parry);

        var ev = new ParrySuccessEvent(attacker, target, attackerWeapon, isPerfect);
        RaiseLocalEvent(target, ev);

        if (_net.IsServer)
        {
            // Stop the empty parry sound
            if (parry.ParrySoundEntity != null)
            {
                _audio.Stop(parry.ParrySoundEntity.Value);
                parry.ParrySoundEntity = null;
            }

            if (isPerfect)
            {
                _audio.PlayPvs(parry.SoundPerfectParry, target);
                _popup.PopupEntity(Loc.GetString("melee-parry-perfect"), target, target, PopupType.Large);
                _popup.PopupEntity(Loc.GetString("melee-parry-blocked"), attacker, attacker, PopupType.MediumCaution);
                _color.RaiseEffect(Color.Yellow, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
                _color.RaiseEffect(Color.Yellow, new List<EntityUid> { attacker }, Filter.Pvs(attacker, entityManager: EntityManager));

                _stun.TrySlowdown(attacker, TimeSpan.FromSeconds(1.0), true, 0f, 0f);

                if (TryComp<StaminaComponent>(target, out _))
                    _stamina.TakeStaminaDamage(target, -parry.PerfectParryStaminaRestore, visual: false);

                RaiseNetworkEvent(new RiposteWindowOpenEvent(GetNetEntity(target), parry.RiposteWindowSeconds));
            }
            else
            {
                _audio.PlayPvs(parry.SoundBlock, target);
                _popup.PopupEntity(Loc.GetString("melee-block-success"), target, target, PopupType.Medium);
                _popup.PopupEntity(Loc.GetString("melee-parry-blocked"), attacker, attacker, PopupType.MediumCaution);
                _color.RaiseEffect(Color.LightBlue, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));

                if (TryComp<StaminaComponent>(target, out _))
                    _stamina.TakeStaminaDamage(target, parry.BlockStaminaDamage, visual: false, source: attacker);
            }

            if (TryComp<StaminaComponent>(attacker, out _))
                _stamina.TakeStaminaDamage(attacker, isPerfect ? 8f : 4f, visual: false, source: target);

            if (_melee.TryGetWeapon(attacker, out var weaponUid, out var weaponComp))
            {
                var staggerTime = isPerfect ? 1.8 : 1.0;
                var staggerEnd = curTime + TimeSpan.FromSeconds(staggerTime);
                if (weaponComp.NextAttack < staggerEnd)
                {
                    weaponComp.NextAttack = staggerEnd;
                    Dirty(weaponUid, weaponComp);
                }
            }
        }

        return true;
    }

    public float GetRiposteMultiplier(EntityUid user)
    {
        if (!TryComp<ParryComponent>(user, out var parry))
            return 1f;

        if (!parry.CanRiposte)
            return 1f;

        var curTime = _timing.CurTime;
        if (curTime > parry.RiposteEndTime)
        {
            parry.CanRiposte = false;
            Dirty(user, parry);
            return 1f;
        }

        parry.CanRiposte = false;
        Dirty(user, parry);

        var riposteMultiplier = parry.RiposteDamageMultiplier;
        if (_melee.TryGetWeapon(user, out var weaponUidForMultiplier, out var weaponForMultiplier))
            riposteMultiplier = weaponForMultiplier.RiposteDamageMultiplierOverride ?? riposteMultiplier;

        if (_net.IsServer)
        {
            _audio.PlayPvs(parry.SoundRiposte, user);
            _popup.PopupEntity(Loc.GetString("melee-riposte-hit"), user, user, PopupType.Large);
            _color.RaiseEffect(Color.Gold, new List<EntityUid> { user }, Filter.Pvs(user, entityManager: EntityManager));

            var weaponNet = NetEntity.Invalid;
            if (_melee.TryGetWeapon(user, out var riposteWeaponUid, out _) && riposteWeaponUid != user)
                weaponNet = GetNetEntity(riposteWeaponUid);

            RaiseNetworkEvent(new RiposteVisualEvent(GetNetEntity(user), weaponNet));
        }

        return riposteMultiplier;
    }

    public void TryPlayFinisher(EntityUid target, EntityUid attacker)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<MobStateComponent>(target, out _))
            return;

        if (_mobState.IsCritical(target))
        {
            _audio.PlayPvs(FinisherSound, target);
            _color.RaiseEffect(Color.DarkRed, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ParryComponent>();

        while (query.MoveNext(out var uid, out var parry))
        {
            if (parry.IsParrying && curTime > parry.ParryEndTime)
            {
                parry.IsParrying = false;
                Dirty(uid, parry);

                if (!parry.ParrySucceeded)
                {
                    if (TryComp<StaminaComponent>(uid, out _))
                    {
                        _stamina.TakeStaminaDamage(uid, parry.FailedParryStaminaDamage, visual: true, source: uid);
                        _popup.PopupEntity(Loc.GetString("melee-parry-failed"), uid, uid, PopupType.SmallCaution);
                    }
                }
            }

            if (parry.CanRiposte && curTime > parry.RiposteEndTime)
            {
                parry.CanRiposte = false;
                Dirty(uid, parry);

                if (_melee.TryGetWeapon(uid, out var weaponUid, out var weaponComp))
                {
                    var staggerEnd = curTime + TimeSpan.FromSeconds(1.2);
                    if (weaponComp.NextAttack < staggerEnd)
                    {
                        weaponComp.NextAttack = staggerEnd;
                        Dirty(weaponUid, weaponComp);
                    }
                }

                _popup.PopupEntity(Loc.GetString("melee-riposte-missed"), uid, uid, PopupType.SmallCaution);
            }
        }
    }
}
