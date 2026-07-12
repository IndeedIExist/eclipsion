using Content.Shared._Crescent.RoundEndCredits;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._Crescent.RoundEndCredits;

public sealed class RoundEndCreditsSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier CreditsMusic = new("/Audio/_Crescent/Task-Force-Friendly-Fire.ogg");

    private RoundEndCreditsWindow? _window;
    private EntityUid? _musicStream;

    public override void Initialize()
    {
        base.Initialize();

        // Disabled until properly implemented.
        // SubscribeNetworkEvent<RoundEndCreditsEvent>(OnCreditsReceived);
    }

    private void OnCreditsReceived(RoundEndCreditsEvent ev)
    {
        _window?.Close();
        StopMusic();

        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _audio.Stop(uid);
        }

        var stream = _audio.PlayGlobal(
            CreditsMusic,
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(2f).WithLoop(true));

        _musicStream = stream?.Entity;

        _window = new RoundEndCreditsWindow(ev.Players, _entManager);
        _window.OnClose += StopMusic;
        _window.OpenCentered();
        _window.MoveToFront();
    }

    private void StopMusic()
    {
        if (_musicStream != null)
        {
            _audio.Stop(_musicStream.Value);
            _musicStream = null;
        }
    }
}
