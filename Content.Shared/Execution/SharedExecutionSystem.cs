using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Execution;

/// <summary>
///     Verb for violently murdering cuffed creatures.
/// </summary>
public sealed class SharedExecutionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSuicideSystem _suicide = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedExecutionSystem _execution = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecutionComponent, GetVerbsEvent<UtilityVerb>>(OnGetInteractionsVerbs);
        SubscribeLocalEvent<ExecutionComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<ExecutionComponent, SuicideByEnvironmentEvent>(OnSuicideByEnvironment);
        SubscribeLocalEvent<ExecutionComponent, ExecutionDoAfterEvent>(OnExecutionDoAfter);
    }

    private void OnGetInteractionsVerbs(EntityUid uid, ExecutionComponent comp, GetVerbsEvent<UtilityVerb> args)
    {
        if (args.Hands == null || args.Using == null || !args.CanAccess || !args.CanInteract)
            return;

        var attacker = args.User;
        var weapon = args.Using.Value;
        var victim = args.Target;

        if (!CanBeExecuted(victim, attacker, weapon))
            return;

        // A crit target gets the flashier "Finish Off" framing; cuffed-but-conscious prisoners keep
        // the plain "Execute" wording.
        var isFinishOff = attacker != victim && _mobState.IsCritical(victim);
        var text = Loc.GetString(isFinishOff ? "execution-verb-finish-off" : "execution-verb-name");

        UtilityVerb verb = new()
        {
            Act = () => TryStartExecutionDoAfter(weapon, victim, attacker, comp),
            Impact = LogImpact.High,
            Text = text,
            Message = Loc.GetString("execution-verb-message"),
        };

        args.Verbs.Add(verb);
    }

    private void TryStartExecutionDoAfter(EntityUid weapon, EntityUid victim, EntityUid attacker, ExecutionComponent comp)
    {
        if (!CanBeExecuted(victim, attacker, weapon))
            return;

        if (attacker == victim)
        {
            ShowExecutionInternalPopup(comp.InternalSelfExecutionMessage, attacker, victim, weapon);
            ShowExecutionExternalPopup(comp.ExternalSelfExecutionMessage, attacker, victim, weapon);
        }
        else if (HasComp<GunComponent>(weapon))
        {
            ShowExecutionInternalPopup(comp.InternalGunExecutionMessage, attacker, victim, weapon);
            ShowExecutionExternalPopup(comp.ExternalGunExecutionMessage, attacker, victim, weapon);
        }
        else
        {
            ShowExecutionInternalPopup(comp.InternalMeleeExecutionMessage, attacker, victim, weapon);
            ShowExecutionExternalPopup(comp.ExternalMeleeExecutionMessage, attacker, victim, weapon);
        }

        var doAfter =
            new DoAfterArgs(EntityManager, attacker, comp.DoAfterDuration, new ExecutionDoAfterEvent(), weapon, target: victim, used: weapon)
            {
                // Cancel if the executioner moves off, the victim is dragged away, or either takes damage.
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                DistanceThreshold = comp.FinishOffRange,
            };

        // Mark the victim so clients draw the red "finishing off" indicator above them for the whole
        // channel. Server-only add; the component networks its presence to viewers. Cleaned up in
        // OnExecutionDoAfter (which fires on both completion and cancellation).
        if (_doAfter.TryStartDoAfter(doAfter) && _net.IsServer && attacker != victim)
            EnsureComp<ExecutionTargetComponent>(victim);
    }

    public bool CanBeExecuted(EntityUid victim, EntityUid attacker, EntityUid weapon)
    {
        // No point executing someone if they can't take damage
        if (!HasComp<DamageableComponent>(victim))
            return false;

        // You can't execute something that cannot die
        if (!TryComp<MobStateComponent>(victim, out var mobState))
            return false;

        // You're not allowed to execute dead people (no fun allowed)
        if (_mobState.IsDead(victim, mobState))
            return false;

        // You must be able to attack people to execute
        if (!_actionBlocker.CanAttack(attacker, victim))
            return false;

        // The victim must be incapacitated to be executed
        if (victim != attacker && _actionBlocker.CanInteract(victim, null))
            return false;

        // All checks passed
        return true;
    }

    private void OnGetMeleeDamage(Entity<ExecutionComponent> entity, ref GetMeleeDamageEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(entity, out var melee) || !entity.Comp.Executing)
            return;

        var bonus = melee.Damage * entity.Comp.DamageMultiplier - melee.Damage;
        args.Damage += bonus;
        args.ResistanceBypass = true;
    }

    private void OnSuicideByEnvironment(Entity<ExecutionComponent> entity, ref SuicideByEnvironmentEvent args)
    {
        if (!TryComp<MeleeWeaponComponent>(entity, out var melee))
            return;

        string? internalMsg = entity.Comp.CompleteInternalSelfExecutionMessage;
        string? externalMsg = entity.Comp.CompleteExternalSelfExecutionMessage;

        if (!TryComp<DamageableComponent>(args.Victim, out var damageableComponent))
            return;

        ShowExecutionInternalPopup(internalMsg, args.Victim, args.Victim, entity, false);
        ShowExecutionExternalPopup(externalMsg, args.Victim, args.Victim, entity);
        _audio.PlayPredicted(melee.SoundHit, args.Victim, args.Victim);
        _suicide.ApplyLethalDamage((args.Victim, damageableComponent), melee.Damage);
        args.Handled = true;
    }

    private void ShowExecutionInternalPopup(string locString, EntityUid attacker, EntityUid victim, EntityUid weapon, bool predict = true)
    {
        if (predict)
        {
            _popup.PopupClient(
               Loc.GetString(locString, ("attacker", attacker), ("victim", victim), ("weapon", weapon)),
               attacker,
               attacker,
               PopupType.MediumCaution
               );
        }
        else
        {
            _popup.PopupEntity(
               Loc.GetString(locString, ("attacker", attacker), ("victim", victim), ("weapon", weapon)),
               attacker,
               attacker,
               PopupType.MediumCaution
               );
        }
    }

    private void ShowExecutionExternalPopup(string locString, EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        _popup.PopupEntity(
            Loc.GetString(locString, ("attacker", attacker), ("victim", victim), ("weapon", weapon)),
            attacker,
            Filter.PvsExcept(attacker),
            true,
            PopupType.MediumCaution
            );
    }

    private void OnExecutionDoAfter(Entity<ExecutionComponent> entity, ref ExecutionDoAfterEvent args)
    {
        // Fires on both completion and cancellation - always clear the visual marker.
        if (_net.IsServer && args.Target is { } markedVictim && HasComp<ExecutionTargetComponent>(markedVictim))
            RemComp<ExecutionTargetComponent>(markedVictim);

        if (args.Handled || args.Cancelled || args.Used == null || args.Target == null)
            return;

        var attacker = args.User;
        var victim = args.Target.Value;
        var weapon = args.Used.Value;

        // Re-validate on completion: the victim must still be executable (they haven't been healed
        // out of crit, died, or moved out of reach).
        if (!_execution.CanBeExecuted(victim, attacker, weapon))
            return;

        // Gun executions fire a point-blank round and guarantee the kill instead of pistol-whipping.
        if (HasComp<GunComponent>(weapon))
        {
            if (TryGunExecute(entity, attacker, victim, weapon))
                args.Handled = true;
            return;
        }

        if (!TryComp<MeleeWeaponComponent>(entity, out var meleeWeaponComp))
            return;

        // This is needed so the melee system does not stop it.
        var prev = _combat.IsInCombatMode(attacker);
        _combat.SetInCombatMode(attacker, true);
        entity.Comp.Executing = true;

        var internalMsg = entity.Comp.CompleteInternalMeleeExecutionMessage;
        var externalMsg = entity.Comp.CompleteExternalMeleeExecutionMessage;

        if (attacker == victim)
        {
            var suicideEvent = new SuicideEvent(victim);
            RaiseLocalEvent(victim, suicideEvent);

            var suicideGhostEvent = new SuicideGhostEvent(victim);
            RaiseLocalEvent(victim, suicideGhostEvent);
        }
        else
            _melee.AttemptLightAttack(attacker, weapon, meleeWeaponComp, victim);

        _combat.SetInCombatMode(attacker, prev);
        entity.Comp.Executing = false;
        args.Handled = true;

        if (attacker != victim)
        {
            _execution.ShowExecutionInternalPopup(internalMsg, attacker, victim, entity);
            _execution.ShowExecutionExternalPopup(externalMsg, attacker, victim, entity);
        }
    }

    /// <summary>
    /// Finishes a critically wounded victim off with a firearm: fires a single point-blank round
    /// and applies guaranteed lethal damage so the victim always dies. Requires the gun to have
    /// something chambered to fire.
    /// </summary>
    private bool TryGunExecute(Entity<ExecutionComponent> gun, EntityUid attacker, EntityUid victim, EntityUid weapon)
    {
        if (!TryComp<GunComponent>(weapon, out var gunComp))
            return false;

        // The client only predicts the do-after; the actual shot and lethal damage happen on the
        // server so we don't double-apply damage or desync ammo.
        if (!_net.IsServer)
            return true;

        if (!_gun.CanShoot(gunComp))
            return false;

        var victimCoords = Transform(victim).Coordinates;
        var projectiles = _gun.AttemptShoot((weapon, gunComp), attacker, victimCoords);

        // Nothing came out of the barrel (empty mag / no round chambered) - no free kill.
        if (projectiles == null || projectiles.Count == 0)
        {
            ShowExecutionInternalPopup(gun.Comp.EmptyGunExecutionMessage, attacker, victim, weapon, false);
            return false;
        }

        // Guarantee the kill regardless of where the point-blank projectile actually ended up.
        if (TryComp<DamageableComponent>(victim, out var damageable))
            _suicide.ApplyLethalDamage((victim, damageable), "Piercing");

        ShowExecutionInternalPopup(gun.Comp.CompleteInternalGunExecutionMessage, attacker, victim, weapon, false);
        ShowExecutionExternalPopup(gun.Comp.CompleteExternalGunExecutionMessage, attacker, victim, weapon);
        return true;
    }
}
