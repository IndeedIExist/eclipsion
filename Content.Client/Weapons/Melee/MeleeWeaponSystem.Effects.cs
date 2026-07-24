using System.Numerics;
using Content.Client.Animations;
using Content.Client.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem
{
    private const string FadeAnimationKey = "melee-fade";
    private const string SlashAnimationKey = "melee-slash";
    private const string ThrustAnimationKey = "melee-thrust";
    private const string ParryAnimationKey = "melee-parry";
    private const string RiposteAnimationKey = "melee-riposte";

    /// <summary>
    /// Does all of the melee effects for a player that are predicted, i.e. character lunge and weapon animation.
    /// </summary>
    public override void DoLunge(EntityUid user, EntityUid weapon, Angle angle, Vector2 localPos, string? animation, bool predicted = true)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var lunge = GetLungeAnimation(localPos);

        // Stop any existing lunges on the user.
        _animation.Stop(user, MeleeLungeKey);
        _animation.Play(user, lunge, MeleeLungeKey);

        if (localPos == Vector2.Zero || animation == null)
            return;

        if (!_xformQuery.TryGetComponent(user, out var userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var animationUid = Spawn(animation, userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite)
            || !TryComp<WeaponArcVisualsComponent>(animationUid, out var arcComponent))
        {
            return;
        }

        var spriteRotation = Angle.Zero;
        if (arcComponent.Animation != WeaponArcAnimation.None
            && TryComp(weapon, out MeleeWeaponComponent? meleeWeaponComponent))
        {
            if (user != weapon
                && TryComp(weapon, out SpriteComponent? weaponSpriteComponent))
                sprite.CopyFrom(weaponSpriteComponent);

            spriteRotation = meleeWeaponComponent.WideAnimationRotation;

            if (meleeWeaponComponent.SwingLeft)
                angle *= -1;
        }
        sprite.Rotation = localPos.ToWorldAngle();
        var distance = Math.Clamp(localPos.Length() / 2f, 0.2f, 1f);

        var xform = _xformQuery.GetComponent(animationUid);
        TrackUserComponent track;

        switch (arcComponent.Animation)
        {
            case WeaponArcAnimation.Slash:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetSlashAnimation(sprite, angle, spriteRotation), SlashAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0.2f, 0.3f), FadeAnimationKey);
                break;
            case WeaponArcAnimation.Thrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetThrustAnimation(sprite, distance, spriteRotation), ThrustAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0.05f, 0.15f), FadeAnimationKey);
                break;
            case WeaponArcAnimation.RapierThrust:
                track = EnsureComp<TrackUserComponent>(animationUid);
                track.User = user;
                _animation.Play(animationUid, GetRapierThrustAnimation(sprite, distance, spriteRotation), ThrustAnimationKey);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0.18f, 0.30f), FadeAnimationKey);
                break;
            case WeaponArcAnimation.None:
                var (mapPos, mapRot) = TransformSystem.GetWorldPositionRotation(userXform);
                var worldPos = mapPos + (mapRot - userXform.LocalRotation).RotateVec(localPos);
                var newLocalPos = Vector2.Transform(worldPos, TransformSystem.GetInvWorldMatrix(xform.ParentUid));
                TransformSystem.SetLocalPositionNoLerp(animationUid, newLocalPos, xform);
                if (arcComponent.Fadeout)
                    _animation.Play(animationUid, GetFadeAnimation(sprite, 0f, 0.15f), FadeAnimationKey);
                break;
        }
    }

    private Animation GetSlashAnimation(SpriteComponent sprite, Angle arc, Angle spriteRotation)
    {
        const float slashStart = 0.08f;
        const float slashEnd = 0.2f;
        const float length = slashEnd + 0.1f;
        var startRotation = sprite.Rotation + arc / 2;
        var endRotation = sprite.Rotation - arc / 2;
        var startRotationOffset = startRotation.RotateVec(new Vector2(0f, -1f));
        var endRotationOffset = endRotation.RotateVec(new Vector2(0f, -1f));
        startRotation += spriteRotation;
        endRotation += spriteRotation;

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRotation, 0f),
                        new AnimationTrackProperty.KeyFrame(startRotation, slashStart),
                        new AnimationTrackProperty.KeyFrame(endRotation, slashEnd)
                    }
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRotationOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(startRotationOffset, slashStart),
                        new AnimationTrackProperty.KeyFrame(endRotationOffset, slashEnd)
                    }
                },
            }
        };
    }

    private Animation GetThrustAnimation(SpriteComponent sprite, float distance, Angle spriteRotation)
    {
        const float thrustEnd = 0.05f;
        const float length = 0.15f;
        var rotation = sprite.Rotation + spriteRotation;
        var startOffset = sprite.Rotation.RotateVec(new Vector2(0f, -distance / 5f));
        var endOffset = sprite.Rotation.RotateVec(new Vector2(0f, -distance));

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(rotation, 0f),
                    }
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(endOffset, thrustEnd),
                        new AnimationTrackProperty.KeyFrame(endOffset, length),
                    }
                },
            }
        };
    }

    private Animation GetRapierThrustAnimation(SpriteComponent sprite, float distance, Angle spriteRotation)
    {
        const float windupEnd = 0.10f;
        const float thrustEnd = 0.18f;
        const float length = 0.30f;

        var rotation = sprite.Rotation + spriteRotation;
        // Use RotateVec like GetThrustAnimation so the offset direction matches the attack direction.
        // Positive Y = backward (pullback), negative Y = forward (thrust).
        var pullbackOffset = sprite.Rotation.RotateVec(new Vector2(0f, distance * 0.25f));
        var endOffset = sprite.Rotation.RotateVec(new Vector2(0f, -distance));

        return new Animation()
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(rotation, 0f),
                    }
                },
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(pullbackOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(pullbackOffset, windupEnd),
                        new AnimationTrackProperty.KeyFrame(endOffset, thrustEnd),
                        new AnimationTrackProperty.KeyFrame(endOffset, length),
                    }
                },
            }
        };
    }

    private Animation GetFadeAnimation(SpriteComponent sprite, float start, float end)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(end),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Color, start),
                        new AnimationTrackProperty.KeyFrame(sprite.Color.WithAlpha(0f), end)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get the sprite offset animation to use for mob lunges.
    /// </summary>
    private Animation GetLungeAnimation(Vector2 direction)
    {
        const float length = 0.1f;

        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty()
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(direction.Normalized() * 0.15f, 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, length)
                    }
                }
            }
        };
    }

    public void DoParryAnimation(EntityUid user)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        if (!_xformQuery.TryGetComponent(user, out var userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var animationUid = Spawn("WeaponArcParry", userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite))
            return;

        var track = EnsureComp<TrackUserComponent>(animationUid);
        track.User = user;

        sprite.Rotation = Angle.FromDegrees(90);

        var parryHoldTime = 0.5f;

        _animation.Stop(animationUid, ParryAnimationKey);
        _animation.Play(animationUid, GetParryAnimation(sprite, parryHoldTime), ParryAnimationKey);
        _animation.Play(animationUid, GetFadeAnimation(sprite, parryHoldTime * 0.7f, parryHoldTime), FadeAnimationKey);
    }

    private Animation GetParryAnimation(SpriteComponent sprite, float holdTime)
    {
        var sideOffset = new Vector2(-0.5f, 0f);
        var readyOffset = new Vector2(-0.4f, -0.15f);
        var rotation = sprite.Rotation;
        const float raiseTime = 0.06f;

        return new Animation
        {
            Length = TimeSpan.FromSeconds(holdTime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(rotation, 0f),
                        new AnimationTrackProperty.KeyFrame(rotation, holdTime),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sideOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(readyOffset, raiseTime),
                        new AnimationTrackProperty.KeyFrame(readyOffset, holdTime),
                    }
                },
            }
        };
    }

    public void DoRiposteAnimation(EntityUid user, EntityUid weapon)
    {
        if (!_xformQuery.TryGetComponent(user, out var userXform) || userXform.MapID == MapId.Nullspace)
            return;

        var animationUid = Spawn("WeaponArcSlash", userXform.Coordinates);

        if (!TryComp<SpriteComponent>(animationUid, out var sprite))
            return;

        sprite.Color = Color.Gold;

        var track = EnsureComp<TrackUserComponent>(animationUid);
        track.User = user;

        _animation.Stop(animationUid, RiposteAnimationKey);
        _animation.Play(animationUid, GetSlashAnimation(sprite, Angle.FromDegrees(100), Angle.Zero), RiposteAnimationKey);
        _animation.Play(animationUid, GetFadeAnimation(sprite, 0.1f, 0.35f), FadeAnimationKey);
    }

    /// <summary>
    /// Updates the effect positions to follow the user
    /// </summary>
    private void UpdateEffects()
    {
        var query = EntityQueryEnumerator<TrackUserComponent, TransformComponent>();
        while (query.MoveNext(out var arcComponent, out var xform))
        {
            // The tracked user may have been deleted or left PVS while the effect entity lingers;
            // GetWorldPosition would otherwise throw KeyNotFoundException on the missing transform every frame.
            if (arcComponent.User is not { } user || !TryComp(user, out TransformComponent? userXform))
                continue;

            Vector2 targetPos = TransformSystem.GetWorldPosition(userXform);

            if (arcComponent.Offset != Vector2.Zero)
            {
                var entRotation = TransformSystem.GetWorldRotation(xform);
                targetPos += entRotation.RotateVec(arcComponent.Offset);
            }

            TransformSystem.SetWorldPosition(xform, targetPos);
        }
    }
}
