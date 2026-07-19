using Content.Shared._Crescent.RoundEnd;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.RoundEnd;

public sealed class FactionRoundEndComputerBoundUserInterface : BoundUserInterface
{
    private FactionRoundEndComputerWindow? _window;

    public FactionRoundEndComputerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FactionRoundEndComputerWindow>();
        _window.OnSubmit += missionId => SendMessage(new FactionRoundEndSubmitMessage(missionId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FactionRoundEndConsoleState consoleState)
            _window?.SetState(consoleState);
    }
}
