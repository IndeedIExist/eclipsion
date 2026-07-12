using Content.Shared._Crescent.SignalTransmitter;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.SignalTransmitter;

public sealed class TransmitterBui : BoundUserInterface
{
    private TransmitterMenu? _menu;

    public TransmitterBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<TransmitterMenu>();

        _menu.OnKeypadButtonPressed += value =>
        {
            SendMessage(new TransmitterKeypadMessage(value));
        };

        _menu.OnClearButtonPressed += () =>
        {
            SendMessage(new TransmitterKeypadClearMessage());
        };

        _menu.OnEnterButtonPressed += () =>
        {
            SendMessage(new TransmitterKeypadEnterMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SignalTransmitterUiState s)
            _menu?.UpdateState(s);
    }
}
