using Content.Shared._Crescent.Audio.CustomBoombox;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Client._Crescent.Audio.CustomBoombox;

public sealed class CustomBoomboxBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClientNetManager _net = default!;

    private CustomBoomboxMenu? _menu;

    public CustomBoomboxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CustomBoomboxMenu>();

        _menu.OnPlayPressed += playing =>
        {
            if (playing)
                SendMessage(new CustomBoomboxPlayingMessage());
            else
                SendMessage(new CustomBoomboxPauseMessage());
        };

        _menu.OnStopPressed += () => SendMessage(new CustomBoomboxStopMessage());

        _menu.SetTime += SetTime;
        _menu.SetVolume += volume => SendMessage(new CustomBoomboxSetVolumeMessage(volume));
        _menu.OnClearPressed += () => SendMessage(new CustomBoomboxClearMessage());

        _menu.OnUploadRequested += (data, name) =>
        {
            _net.ClientSendMessage(new MsgCustomBoomboxUpload
            {
                Boombox = EntMan.GetNetEntity(Owner),
                Data = data,
                FileName = name
            });
        };

        Reload();
    }

    public void Reload()
    {
        if (_menu == null || !EntMan.TryGetComponent(Owner, out CustomBoomboxComponent? boom))
            return;

        _menu.SetAudioStream(boom.AudioStream);

        _menu.SetVolumeValue(boom.Volume);

        if (boom.SelectedTrackResourcePath is { } path && boom.SelectedTrackDisplayName is { } display)
        {
            var length = EntMan.System<AudioSystem>().GetAudioLength(new ResolvedPathSpecifier(path));
            _menu.SetSelectedSong(display, (float) length.TotalSeconds);
        }
        else
        {
            _menu.SetSelectedSong(string.Empty, 0f);
        }
    }

    private void SetTime(float time)
    {
        SendMessage(new CustomBoomboxSetTimeMessage(time));
    }
}
