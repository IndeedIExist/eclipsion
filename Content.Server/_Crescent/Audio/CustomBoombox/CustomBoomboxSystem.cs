using System;
using System.IO;
using Content.Shared._Crescent.Audio.CustomBoombox;
using Content.Shared.Audio.Jukebox;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Server.Upload;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Upload;
using Robust.Shared.Utility;

namespace Content.Server._Crescent.Audio.CustomBoombox;

public sealed class CustomBoomboxSystem : SharedCustomBoomboxSystem
{
    private const int MaxUploadBytes = 5 * 1024 * 1024;
    private static ReadOnlySpan<byte> OggMagic => "OggS"u8;

    private readonly MemoryContentRoot _serverUploadedRoot = new();

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceManager _resourceMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        _resourceMan.AddRoot(new ResPath("/Uploaded"), _serverUploadedRoot);

        _net.RegisterNetMessage<MsgCustomBoomboxUpload>(OnUploadNet);

        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxPlayingMessage>(OnPlay);
        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxPauseMessage>(OnPause);
        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxStopMessage>(OnStop);
        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxSetTimeMessage>(OnSetTime);
        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxSetVolumeMessage>(OnSetVolume);
        SubscribeLocalEvent<CustomBoomboxComponent, CustomBoomboxClearMessage>(OnClear);
        SubscribeLocalEvent<CustomBoomboxComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CustomBoomboxComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, CustomBoomboxComponent component, ComponentStartup args)
    {
        _appearance.SetData(uid, JukeboxVisuals.VisualState, JukeboxVisualState.On);
    }

    private void OnShutdown(EntityUid uid, CustomBoomboxComponent component, ComponentShutdown args)
    {
        StopAndCleanup(uid, component);
    }

    private void OnUploadNet(MsgCustomBoomboxUpload msg)
    {
        if (!_playerManager.TryGetSessionByChannel(msg.MsgChannel, out var session) ||
            session.AttachedEntity is not { } user)
        {
            return;
        }

        var uid = GetEntity(msg.Boombox);
        if (!TryComp<CustomBoomboxComponent>(uid, out var component))
            return;

        if (!_interaction.InRangeUnobstructed(user, uid))
            return;

        if (msg.Data.Length == 0 || msg.Data.Length > MaxUploadBytes)
            return;

        if (msg.Data.Length < OggMagic.Length || !msg.Data.AsSpan().StartsWith(OggMagic))
            return;

        var baseName = Path.GetFileName(msg.FileName);
        if (string.IsNullOrEmpty(baseName) || !baseName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            return;

        if (baseName.Length > 80)
            baseName = baseName[..80];

        var wasPlaying = Audio.IsPlaying(component.AudioStream);

        // Stop current stream and remove old file from memory
        CleanupAudioFile(uid, component);
        component.AudioStream = Audio.Stop(component.AudioStream);
        component.AutoPlayAfterSelect = false;

        component.UploadRevision++;
        var rel = new ResPath($"custom_boombox/b{uid.Id}_{component.UploadRevision}.ogg").ToRelativePath();
        _net.ServerSendToAll(new NetworkResourceUploadMessage { RelativePath = rel, Data = msg.Data });

        var fullPath = $"/Uploaded/custom_boombox/b{uid.Id}_{component.UploadRevision}.ogg";
        component.SelectedTrackResourcePath = fullPath;
        component.SelectedTrackDisplayName = baseName;

        _serverUploadedRoot.AddOrUpdateFile(rel, msg.Data);

        if (wasPlaying)
        {
            component.AudioStream = Audio.PlayPvs(
                new SoundPathSpecifier(fullPath), uid,
                AudioParams.Default.WithMaxDistance(10f).WithVolume(GetVolumeDb(component.Volume)))?.Entity;
        }

        DirectSetVisualState(uid, JukeboxVisualState.Select);
        component.Selecting = true;
        component.SelectAccumulator = 0f;

        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CustomBoomboxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Selecting)
                continue;

            comp.SelectAccumulator += frameTime;
            if (comp.SelectAccumulator < 0.5f)
                continue;

            comp.SelectAccumulator = 0f;
            comp.Selecting = false;
            TryUpdateVisualState(uid, comp);
        }
    }

    private void OnPlay(EntityUid uid, CustomBoomboxComponent component, ref CustomBoomboxPlayingMessage args)
    {
        if (Exists(component.AudioStream))
        {
            Audio.SetState(component.AudioStream, AudioState.Playing);
            return;
        }

        if (component.SelectedTrackResourcePath is not { } pathStr)
            return;

        component.AudioStream = Audio.PlayPvs(
            new SoundPathSpecifier(pathStr), uid,
            AudioParams.Default.WithMaxDistance(10f).WithVolume(GetVolumeDb(component.Volume)))?.Entity;

        Dirty(uid, component);
    }

    private void OnPause(Entity<CustomBoomboxComponent> ent, ref CustomBoomboxPauseMessage args)
    {
        Audio.SetState(ent.Comp.AudioStream, AudioState.Paused);
    }

    private static float GetVolumeDb(float volumePercent)
        => -30f + volumePercent * 0.3f;

    private void OnSetVolume(EntityUid uid, CustomBoomboxComponent component, CustomBoomboxSetVolumeMessage args)
    {
        component.Volume = Math.Clamp(args.Volume, 0f, 100f);
        Audio.SetVolume(component.AudioStream, GetVolumeDb(component.Volume));
        Dirty(uid, component);
    }

    private void OnClear(EntityUid uid, CustomBoomboxComponent component, CustomBoomboxClearMessage args)
    {
        StopAndCleanup(uid, component);
        component.SelectedTrackResourcePath = null;
        component.SelectedTrackDisplayName = null;
        TryUpdateVisualState(uid, component);
        Dirty(uid, component);
    }

    private void OnSetTime(EntityUid uid, CustomBoomboxComponent component, CustomBoomboxSetTimeMessage args)
    {
        if (TryComp(args.Actor, out ActorComponent? actorComp))
        {
            var offset = actorComp.PlayerSession.Channel.Ping * 1.5f / 1000f;
            Audio.SetPlaybackPosition(component.AudioStream, args.SongTime + offset);
        }
    }

    private void OnStop(Entity<CustomBoomboxComponent> entity, ref CustomBoomboxStopMessage args)
    {
        entity.Comp.AudioStream = Audio.Stop(entity.Comp.AudioStream);
        Dirty(entity);
    }

    /// <summary>
    /// Stops audio and removes uploaded file from server memory.
    /// </summary>
    private void StopAndCleanup(EntityUid uid, CustomBoomboxComponent component)
    {
        component.AudioStream = Audio.Stop(component.AudioStream);
        component.AutoPlayAfterSelect = false;
        CleanupAudioFile(uid, component);
    }

    /// <summary>
    /// Removes uploaded audio file from server memory.
    /// </summary>
    private void CleanupAudioFile(EntityUid uid, CustomBoomboxComponent component)
    {
        if (component.UploadRevision > 0)
        {
            var oldRel = new ResPath($"custom_boombox/b{uid.Id}_{component.UploadRevision}.ogg").ToRelativePath();
            _serverUploadedRoot.RemoveFile(oldRel);
        }
    }

    private void DirectSetVisualState(EntityUid uid, JukeboxVisualState state)
    {
        _appearance.SetData(uid, JukeboxVisuals.VisualState, state);
    }

    private void TryUpdateVisualState(EntityUid uid, CustomBoomboxComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        _appearance.SetData(uid, JukeboxVisuals.VisualState, JukeboxVisualState.On);
    }
}
