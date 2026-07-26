using System.Linq;
using Content.Client.Gameplay;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    private const string MeleeLungeKey = "melee-lunge";

    // Soft (middle-click) melee attacks are deliberately slow so they can't be spammed like the
    // left-click wide slash - a single, committed poke roughly once every couple of seconds.
    private static readonly TimeSpan SoftAttackCooldown = TimeSpan.FromSeconds(2);
    private TimeSpan _nextSoftAttack;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        SubscribeNetworkEvent<ParryVisualEvent>(OnParryVisual);
        SubscribeNetworkEvent<RiposteVisualEvent>(OnRiposteVisual);
        SubscribeNetworkEvent<RiposteWindowOpenEvent>(OnRiposteWindowOpen);
        SubscribeNetworkEvent<MeleeImpactEffectEvent>(OnMeleeImpactEffect);
        UpdatesOutsidePrediction = true;
        InitializeShake();
    }

    /// <summary>
    /// Victim's side of a landed hit (screen shake) - the attacker already got this locally via <see cref="DoDamageEffect"/>.
    /// </summary>
    private void OnMeleeImpactEffect(MeleeImpactEffectEvent ev)
    {
        foreach (var netTarget in ev.Targets)
        {
            var target = GetEntity(netTarget);
            if (!Exists(target))
                continue;

            if (target == _player.LocalEntity)
                TriggerMeleeShake(0.07f);
        }
    }

    private void OnParryVisual(ParryVisualEvent ev)
    {
        var ent = GetEntity(ev.Parrier);
        if (!Exists(ent))
            return;

        DoParryAnimation(ent);
    }

    private void OnRiposteWindowOpen(RiposteWindowOpenEvent ev)
    {
        var ent = GetEntity(ev.Parrier);
        if (!Exists(ent))
            return;

        // Perfect parry already gets a popup ("Strike now for a riposte!") from SharedParrySystem;
        // this is just the local screen kick for whoever actually earned it.
        if (ent == _player.LocalEntity)
            TriggerMeleeShake(0.12f);
    }

    private void OnRiposteVisual(RiposteVisualEvent ev)
    {
        var ent = GetEntity(ev.Attacker);
        if (!Exists(ent))
            return;

        _color.RaiseEffect(Color.Gold, new List<EntityUid> { ent }, Filter.Local());

        if (ent == _player.LocalEntity)
            TriggerMeleeShake(0.1f);

        var weapon = GetEntity(ev.Weapon);
        if (weapon.Valid && Exists(weapon))
            DoRiposteAnimation(ent, weapon);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
        UpdateMeleeShake(frameTime);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        if (!TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(entity) || !Blocker.CanAttack(entity))
        {
            weapon.Attacking = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;

        // Disregard inputs to the shoot binding
        if (TryComp<GunComponent>(weaponUid, out var gun)
            && (!HasComp<GunRequiresWieldComponent>(weaponUid)
            || TryComp<WieldableComponent>(weaponUid, out var wieldable)
            && wieldable.Wielded))
        {
            if (gun.UseKey)
                useDown = false;
            else
                altDown = false;
        }

        // Right-click: Parry attempt (requires holding a melee weapon, not unarmed or gun)
        if (altDown && !useDown)
        {
            if (weaponUid != entity
                && !HasComp<GunComponent>(weaponUid)
                && TryComp<ParryComponent>(entity, out var parry)
                && !parry.IsParrying
                && parry.NextParryTime <= Timing.CurTime)
            {
                RaisePredictiveEvent(new ParryAttemptEvent(GetNetEntity(weaponUid)));
                DoParryAnimation(entity);
            }
            return;
        }

        // Middle-click: soft (light) attack for a held weapon - a single-target poke on a long
        // cooldown so it can't be spammed like the left-click wide slash. For melee weapons this is
        // the soft attack; for a gun (whose weapon entity carries a melee component) it's a
        // pistol-whip / bash. Left-click still fires the gun as normal.
        var middleDown = _inputSystem.CmdStates.GetState(ContentKeyFunctions.MouseMiddle) == BoundKeyState.Down;
        if (middleDown
            && weaponUid != entity
            && !weapon.Attacking
            && weapon.NextAttack <= Timing.CurTime
            && Timing.CurTime >= _nextSoftAttack)
        {
            var softPos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
            var softCoords = TransformSystem.ToCoordinates(softPos);

            EntityUid? softTarget = null;
            if (_stateManager.CurrentState is GameplayStateBase softScreen)
                softTarget = softScreen.GetClickedEntity(softPos);

            RaisePredictiveEvent(new LightAttackEvent(
                softTarget != null ? GetNetEntity(softTarget.Value) : null,
                GetNetEntity(weaponUid),
                GetNetCoordinates(softCoords)));

            _nextSoftAttack = Timing.CurTime + SoftAttackCooldown;
            return;
        }

        if ((weapon.AutoAttack || !useDown) && weapon.Attacking)
            RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime || !useDown)
            return;

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);
        var coordinates = TransformSystem.ToCoordinates(mousePos);

        // Fists (unarmed - the "weapon" is the mob itself) do a precise single-target light attack
        // instead of the wide heavy swing, so a bare-handed left-click is just a straight punch.
        if (weaponUid == entity)
        {
            EntityUid? target = null;
            if (_stateManager.CurrentState is GameplayStateBase screen)
                target = screen.GetClickedEntity(mousePos);

            RaisePredictiveEvent(new LightAttackEvent(
                target != null ? GetNetEntity(target.Value) : null,
                GetNetEntity(weaponUid),
                GetNetCoordinates(coordinates)));
            return;
        }

        // Left-click: Wide slash attack for held melee weapons.
        if (!weapon.DisableHeavy && useDown)
        {
            ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
            return;
        }
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        _color.RaiseEffect(Color.Red, targets, Filter.Local());

        // Instant local feedback for the attacker (this only runs on the attacker's own client, via
        // prediction). The victim gets the same shake over the network - see OnMeleeImpactEffect.
        if (user == _player.LocalEntity)
            TriggerMeleeShake(0.045f);
    }

    protected override bool DoDisarm(EntityUid user, DisarmAttackEvent ev, EntityUid meleeUid, MeleeWeaponComponent component, ICommonSession? session)
    {
        if (!base.DoDisarm(user, ev, meleeUid, component, session)
            || !TryComp<CombatModeComponent>(user, out var combatMode)
            || combatMode.CanDisarm != true)
            return false;

        var target = GetEntity(ev.Target);

        // They need to either have hands...
        if (!HasComp<HandsComponent>(target!.Value))
        {
            // or just be able to be shoved over.
            if (TryComp<StatusEffectsComponent>(target, out var status) && status.AllowedEffects.Contains("KnockedDown"))
                return true;

            if (Timing.IsFirstTimePredicted && HasComp<MobStateComponent>(target.Value))
                PopupSystem.PopupEntity(Loc.GetString("disarm-action-disarmable", ("targetName", target.Value)), target.Value);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!_xformQuery.TryGetComponent(user, out var userXform)
            || !Timing.IsFirstTimePredicted)
            return;

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);
        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(component.Range * component.HeavyRangeModifier, direction.Length());

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).ToList());
        RaisePredictiveEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(component.MaxTargets, entities.Count)), GetNetCoordinates(coordinates)));
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        if (!Exists(ent))
            return;

        DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation, false);
    }
}
