using Content.Shared._Art.CVars;
using Content.Shared._Art.TTS;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Art.TTS;

/// <summary>
/// Plays TTS audio in world
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill _sawmill = default!;
    private readonly MemoryContentRoot _contentRoot = new();
    private ResPath _prefix;

    /// <summary>
    /// Volume reduction for whispered TTS (converted to logarithmic scale)
    /// </summary>
    private const float WhisperVolumeReduction = 4f;

    /// <summary>
    /// Decibel bounds for playback. Anything at or below <see cref="MinVolume"/> is already inaudible, so the
    /// clamp only ever discards values that could not have been heard anyway.
    /// </summary>
    private const float MinVolume = -100f;

    /// <inheritdoc cref="MinVolume"/>
    private const float MaxVolume = 10f;

    /// <summary>
    /// Grace period added on top of a clip's own length before its buffer is freed, so a stream is never
    /// pulled out from under a source that is still reading it.
    /// </summary>
    private static readonly TimeSpan ReleaseGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Decoded TTS buffers waiting to be freed once playback has finished.
    ///
    /// Every line of speech decodes into its own OpenAL buffer, and nothing else owns it: the audio entity
    /// that plays it is cleaned up on its own, but the buffer behind it is not. Left alone these accumulate
    /// for the whole session, which on a talkative round is thousands of buffers of native memory.
    /// </summary>
    private readonly List<(AudioStream Stream, TimeSpan FreeAt)> _pendingRelease = new();

    private float _volume = 0.0f;
    private ulong _fileIdx = 0;
    private static ulong _shareIdx = 0;

    public override void Initialize()
    {
        _prefix = ResPath.Root / $"TTS{_shareIdx++}";
        _sawmill = Logger.GetSawmill("tts");
        _res.AddRoot(_prefix, _contentRoot);
        _cfg.OnValueChanged(ArtCVars.TTSVolume, OnTtsVolumeChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        ReleaseAllStreams();
        _contentRoot.Clear();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(ArtCVars.TTSVolume, OnTtsVolumeChanged);
        ReleaseAllStreams();

        // Deliberately cleared rather than disposed. IResourceManager has an AddRoot but no RemoveRoot, so the
        // root registered in Initialize stays mounted for the life of the process even though this system is
        // recreated on every reconnect. Disposing the backing store would leave a mounted root whose
        // ReaderWriterLockSlim is dead, and any lookup that reached it would throw ObjectDisposedException.
        _contentRoot.Clear();
    }

    public void RequestGlobalTTS(VoiceRequestType text, string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        // Gain of 0 would become -infinity dB below, so skip the work entirely instead of decoding audio
        // nobody can hear. Players who muted TTS, or who still have the old 0 default archived, land here.
        // A non-finite cvar is treated the same way: there is no sane volume to play it at.
        if (!float.IsFinite(_volume) || _volume <= 0f)
            return;

        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        _contentRoot.AddOrUpdateFile(filePath, ev.Data);

        var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, _prefix / filePath);
        var stream = audioResource.AudioStream;

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        var played = false;
        if (ev.SourceUid != null)
        {
            var sourceUid = GetEntity(ev.SourceUid.Value);
            if (sourceUid.IsValid())
                played = _audio.PlayEntity(stream, sourceUid, null, audioParams) != null;
        }
        else
        {
            played = _audio.PlayGlobal(stream, null, audioParams) != null;
        }

        _contentRoot.RemoveFile(filePath);

        // Nothing ever plays a TTS buffer twice, so it can be freed as soon as this one playback is done.
        if (played)
            _pendingRelease.Add((stream, _timing.RealTime + stream.Length + ReleaseGrace));
        else
            stream.Dispose();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_pendingRelease.Count == 0)
            return;

        var now = _timing.RealTime;
        for (var i = _pendingRelease.Count - 1; i >= 0; i--)
        {
            if (_pendingRelease[i].FreeAt > now)
                continue;

            _pendingRelease[i].Stream.Dispose();
            _pendingRelease.RemoveAt(i);
        }
    }

    private void ReleaseAllStreams()
    {
        foreach (var (stream, _) in _pendingRelease)
        {
            stream.Dispose();
        }

        _pendingRelease.Clear();
    }

    /// <summary>
    /// The playback volume in decibels.
    /// </summary>
    /// <remarks>
    /// The result is clamped to a finite range before it leaves this method. <see cref="SharedAudioSystem.GainToVolume"/>
    /// is a log10, so a gain of 0 comes back as -infinity, and the audio backend converts whatever it is
    /// handed straight into an OpenAL gain. Passing a non-finite value through is undefined behaviour down
    /// there, and it manifests differently depending on the driver rather than failing cleanly.
    /// </remarks>
    private float AdjustVolume(bool isWhisper)
    {
        var volume = SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
            volume -= SharedAudioSystem.GainToVolume(WhisperVolumeReduction);

        // -100 dB is already inaudible, so clamping costs nothing audible and keeps the value finite.
        return float.IsFinite(volume) ? Math.Clamp(volume, MinVolume, MaxVolume) : MinVolume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }
}
