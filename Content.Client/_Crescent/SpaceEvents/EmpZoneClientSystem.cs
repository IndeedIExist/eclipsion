using System.Numerics;
using Content.Shared._Crescent.SpaceEvents;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Crescent.SpaceEvents;

public sealed class EmpZoneClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<int, (Vector2 Center, float Radius)> _activeZones = new();
    public IReadOnlyDictionary<int, (Vector2 Center, float Radius)> ActiveZones => _activeZones;
    public bool ZoneActive => _activeZones.Count > 0;

    private EmpZoneScreenOverlay _overlay = default!;
    private bool _playerInZone = false;

    private float _checkTimer = 0f;
    private const float CheckInterval = 5f;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new EmpZoneScreenOverlay();
        SubscribeNetworkEvent<EmpZoneActivatedEvent>(OnZoneActivated);
        SubscribeNetworkEvent<EmpZoneDeactivatedEvent>(OnZoneDeactivated);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnZoneActivated(EmpZoneActivatedEvent ev)
    {
        _activeZones[ev.ZoneId] = (ev.Center, ev.Radius);
        _checkTimer = CheckInterval;
    }

    private void OnZoneDeactivated(EmpZoneDeactivatedEvent ev)
    {
        _activeZones.Remove(ev.ZoneId);
        if (_activeZones.Count == 0)
            RemoveOverlayIfNeeded();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeZones.Clear();
        RemoveOverlayIfNeeded();
    }

    private void RemoveOverlayIfNeeded()
    {
        if (!_playerInZone)
            return;
        _playerInZone = false;
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!ZoneActive)
            return;

        _checkTimer += frameTime;
        if (_checkTimer < CheckInterval)
            return;
        _checkTimer = 0f;

        var player = _playerMan.LocalEntity;
        if (player == null)
        {
            RemoveOverlayIfNeeded();
            return;
        }

        var worldPos = _transform.GetWorldPosition(player.Value);

        var inAnyZone = false;
        foreach (var (_, (center, radius)) in _activeZones)
        {
            if ((worldPos - center).Length() <= radius)
            {
                inAnyZone = true;
                break;
            }
        }

        if (inAnyZone && !_playerInZone)
        {
            _playerInZone = true;
            _overlayMan.AddOverlay(_overlay);
        }
        else if (!inAnyZone && _playerInZone)
        {
            _playerInZone = false;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }
}