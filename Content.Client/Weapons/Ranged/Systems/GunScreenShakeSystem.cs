using System;
using System.Numerics;
using Content.Shared._Crescent.Weapons;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Weapons.Ranged.Systems;

/// <summary>
/// Replaces the directional camera-kick shake with a brief oscillating burst.
/// Triggers from GunShotEvent (client-predicted, immediate) and CameraKickEvent
/// (server-confirmed, covers explosions and other sources too).
/// Runs after SharedCameraRecoilSystem so our final SetOffset wins each frame.
///
/// Also adds a sustained micro-vibration layer for automatic fire: every 3rd shot
/// starts a tiny continuous tremble that persists as long as rapid fire continues.
/// </summary>
public sealed class GunScreenShakeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager        _playerManager = default!;
    [Dependency] private readonly IGameTiming           _timing        = default!;
    [Dependency] private readonly IConfigurationManager _cfg           = default!;

    private SharedEyeSystem _eyeSystem = default!;
    private bool _shakeEnabled = true;

    // --- Regular per-shot shake ---
    private const float ShakeDuration  = 0.12f;
    private const float ShakeCooldown  = 0.40f;
    private const float FreqX          = 11f;
    private const float FreqY          = 8.5f;
    private const float MagnitudeScale = 0.4f;
    private const float MagnitudeMax   = 0.15f;

    private float _remaining = 0f;
    private float _elapsed   = 0f;
    private float _magnitude = 0f;
    private float _phaseX    = 0f;
    private float _phaseY    = 0f;
    private float _cooldown  = 0f;

    // --- Sustained auto-fire micro-vibration ---
    // Triggers every AutoFireEvery shots; stays active while rapid fire continues.
    private const int   AutoFireEvery       = 3;     // how many shots before a vibration trigger
    private const float VibeTimeoutDuration = 0.28f; // how long vibe stays active after each trigger
    private const float VibeFadeOut         = 0.07f; // smooth fade-out window at end of timeout
    private const float VibeMagnitude       = 0.014f; // world-unit amplitude — very subtle
    private const float VibeFreqX           = 24f;
    private const float VibeFreqY           = 19f;

    private int   _autoShotCount = 0;
    private float _vibeTimeout   = 0f;
    private float _vibeElapsed   = 0f;
    private float _vibePhaseX    = 0f;
    private float _vibePhaseY    = 0f;

    private readonly Random _rng = new();

    public override void Initialize()
    {
        base.Initialize();
        _eyeSystem = EntityManager.System<SharedEyeSystem>();

        Subs.CVar(_cfg, CCVars.ScreenShakeEnabled, v => _shakeEnabled = v, true);

        // Run after SharedCameraRecoilSystem so our offset wins the frame
        UpdatesAfter.Add(typeof(Content.Client.Camera.CameraRecoilSystem));

        // GunShotEvent: fires during client prediction — immediate, zero latency
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);

        // CameraKickEvent: server-confirmed, also covers explosions on the same grid
        SubscribeNetworkEvent<CameraKickEvent>(OnCameraKick);
    }

    private void OnGunShot(EntityUid uid, GunComponent gun, ref GunShotEvent args)
    {
        if (!_shakeEnabled) return;
        if (!_timing.IsFirstTimePredicted) return;
        if (args.User != _playerManager.LocalEntity) return;

        // Per-weapon magnitude via GunCameraShakeComponent; fall back to scalar formula.
        var magnitude = TryComp<GunCameraShakeComponent>(uid, out var shakeComp)
            ? shakeComp.Magnitude
            : gun.CameraRecoilScalarModified * 0.09f;
        StartShake(MathF.Min(magnitude, MagnitudeMax));

        // Sustained vibration: accumulate shots, trigger every 3rd
        _autoShotCount++;
        if (_autoShotCount >= AutoFireEvery)
        {
            _autoShotCount = 0;

            if (_vibeTimeout <= 0f)
            {
                // Fresh start — randomise phase so it doesn't feel mechanical
                _vibePhaseX  = _rng.NextSingle() * MathF.Tau;
                _vibePhaseY  = _rng.NextSingle() * MathF.Tau;
                _vibeElapsed = 0f;
            }

            _vibeTimeout = VibeTimeoutDuration; // refresh / extend the active window
        }
    }

    private void OnCameraKick(CameraKickEvent ev)
    {
        if (!_shakeEnabled) return;
        var player = _playerManager.LocalEntity;
        if (player == null) return;
        if (GetEntity(ev.NetEntity) != player.Value) return;

        StartShake(MathF.Min(ev.Recoil.Length() * MagnitudeScale, MagnitudeMax));
    }

    private void StartShake(float magnitude)
    {
        if (magnitude <= 0f) return;
        if (_cooldown > 0f) return; // rapid fire: skip until cooldown expires

        _magnitude = magnitude;
        _remaining = ShakeDuration;
        _elapsed   = 0f;
        _cooldown  = ShakeCooldown;
        _phaseX    = _rng.NextSingle() * MathF.Tau;
        _phaseY    = _rng.NextSingle() * MathF.Tau;
    }

    public override void FrameUpdate(float frameTime)
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
        {
            _remaining     = 0f;
            _vibeTimeout   = 0f;
            _autoShotCount = 0;
            return;
        }

        if (!TryComp<EyeComponent>(player.Value, out var eye)) return;

        if (_cooldown > 0f)
            _cooldown -= frameTime;

        var shakeOffset = Vector2.Zero;

        // --- Regular per-shot shake ---
        if (_remaining > 0f)
        {
            _remaining -= frameTime;
            _elapsed   += frameTime;

            if (_remaining > 0f)
            {
                var decay = _remaining / ShakeDuration;
                shakeOffset += new Vector2(
                    MathF.Sin(_elapsed * FreqX * MathF.Tau + _phaseX) * _magnitude * decay,
                    MathF.Sin(_elapsed * FreqY * MathF.Tau + _phaseY) * _magnitude * decay
                );
            }
        }

        // --- Sustained micro-vibration ---
        if (_vibeTimeout > 0f)
        {
            _vibeTimeout -= frameTime;
            _vibeElapsed += frameTime;

            if (_vibeTimeout <= 0f)
            {
                // Window expired — reset counter so next burst starts fresh
                _autoShotCount = 0;
            }
            else
            {
                // Smooth fade-out in the last VibeFadeOut seconds of the window
                var alpha = MathF.Min(1f, _vibeTimeout / VibeFadeOut);
                shakeOffset += new Vector2(
                    MathF.Sin(_vibeElapsed * VibeFreqX * MathF.Tau + _vibePhaseX) * VibeMagnitude * alpha,
                    MathF.Sin(_vibeElapsed * VibeFreqY * MathF.Tau + _vibePhaseY) * VibeMagnitude * alpha
                );
            }
        }

        // When no active shake, don't touch the offset — telescope and recoil systems own it.
        if (shakeOffset == Vector2.Zero)
            return;

        // Add shake oscillation on top of the base offset (telescope look-ahead) + current recoil kick.
        // Never overwrite recoil.BaseOffset — that belongs to the telescope system.
        if (TryComp<CameraRecoilComponent>(player.Value, out var recoil))
            _eyeSystem.SetOffset(player.Value, recoil.BaseOffset + recoil.CurrentKick + shakeOffset, eye);
        else
            _eyeSystem.SetOffset(player.Value, eye.Offset + shakeOffset, eye);
    }
}
