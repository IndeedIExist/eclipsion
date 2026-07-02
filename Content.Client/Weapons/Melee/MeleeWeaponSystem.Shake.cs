using System;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Camera;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Client.Weapons.Melee;

/// <summary>
/// Small, self-contained camera shake for melee "juice" - a confirmed hit, a perfect parry or a
/// riposte give the local player's screen a brief kick so combat reads as more impactful.
/// Deliberately independent of <see cref="Content.Client.Camera.CameraRecoilSystem"/>/guns so melee
/// doesn't need the ranged-weapon screen shake plumbing to feel good.
/// </summary>
public sealed partial class MeleeWeaponSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private SharedEyeSystem _eye = default!;
    private readonly Random _shakeRng = new();
    private bool _shakeEnabled = true;

    private float _shakeMagnitude;
    private float _shakeDuration;
    private float _shakeRemaining;
    private float _shakePhaseX;
    private float _shakePhaseY;

    private const float ShakeFreqX = 14f;
    private const float ShakeFreqY = 10f;

    private void InitializeShake()
    {
        _eye = EntityManager.System<SharedEyeSystem>();
        Subs.CVar(_cfg, CCVars.ScreenShakeEnabled, v =>
        {
            _shakeEnabled = v;
            if (!v)
                _shakeRemaining = 0f;
        }, true);
    }

    /// <summary>
    /// Kicks the local screen briefly. Ignored if a stronger shake is already playing, or if the
    /// player has screen shake disabled in accessibility options.
    /// </summary>
    private void TriggerMeleeShake(float magnitude, float duration = 0.15f)
    {
        if (!_shakeEnabled)
            return;

        if (_shakeRemaining > 0f && _shakeMagnitude >= magnitude)
            return;

        _shakeMagnitude = magnitude;
        _shakeDuration = duration;
        _shakeRemaining = duration;
        _shakePhaseX = _shakeRng.NextSingle() * MathF.Tau;
        _shakePhaseY = _shakeRng.NextSingle() * MathF.Tau;
    }

    private void UpdateMeleeShake(float frameTime)
    {
        if (!_shakeEnabled || _shakeRemaining <= 0f)
            return;

        var player = _player.LocalEntity;
        if (player == null || !TryComp<EyeComponent>(player.Value, out var eye))
        {
            _shakeRemaining = 0f;
            return;
        }

        _shakeRemaining -= frameTime;
        var elapsed = _shakeDuration - _shakeRemaining;
        var decay = MathF.Max(_shakeRemaining, 0f) / _shakeDuration;

        var shakeOffset = new Vector2(
            MathF.Sin(elapsed * ShakeFreqX * MathF.Tau + _shakePhaseX) * _shakeMagnitude * decay,
            MathF.Sin(elapsed * ShakeFreqY * MathF.Tau + _shakePhaseY) * _shakeMagnitude * decay);

        if (TryComp<CameraRecoilComponent>(player.Value, out var recoil))
            _eye.SetOffset(player.Value, recoil.BaseOffset + recoil.CurrentKick + shakeOffset, eye);
        else
            _eye.SetOffset(player.Value, eye.Offset + shakeOffset, eye);
    }
}
