using Content.Shared._Crescent.Research.TechShare;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Research.TechShare;

public sealed class TechShareReceiverBui : BoundUserInterface
{
    private TechShareReceiverWindow? _window;

    public TechShareReceiverBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TechShareReceiverWindow>();

        _window.OnAccept += () =>
        {
            SendMessage(new TechShareAcceptMessage());
        };

        _window.OnReject += () =>
        {
            SendMessage(new TechShareRejectMessage());
        };

        _window.OnDisconnect += () =>
        {
            SendMessage(new TechShareDisconnectReceiverMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is TechShareReceiverBuiState s)
            _window?.UpdateState(s);
    }
}
