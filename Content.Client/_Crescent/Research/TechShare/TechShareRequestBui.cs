using Content.Shared._Crescent.Research.TechShare;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Research.TechShare;

public sealed class TechShareRequestBui : BoundUserInterface
{
    private TechShareRequestWindow? _window;

    public TechShareRequestBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TechShareRequestWindow>();

        _window.OnSendRequest += (target, recipes, mode, durationOrCount) =>
        {
            SendMessage(new TechShareSendRequestMessage(target, recipes, mode, durationOrCount));
        };

        _window.OnCancelRequest += () =>
        {
            SendMessage(new TechShareCancelRequestMessage());
        };

        _window.OnDisconnect += () =>
        {
            SendMessage(new TechShareDisconnectRequestMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is TechShareRequestBuiState s)
            _window?.UpdateState(s);
    }
}
