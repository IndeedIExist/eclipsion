using Content.Shared._Crescent.Factions;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Factions;

public sealed class FactionRecruitmentConsoleBoundUserInterface : BoundUserInterface
{
    private FactionRecruitmentWindow? _window;

    public FactionRecruitmentConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FactionRecruitmentWindow>();
        _window.OnAssign += (target, jobId) => SendMessage(new FactionRecruitmentAssignMessage(target, jobId));
        _window.OnDismiss += target => SendMessage(new FactionRecruitmentDismissMessage(target));
        _window.OnRefresh += () => SendMessage(new FactionRecruitmentRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FactionRecruitmentConsoleState recruitmentState)
            _window?.UpdateState(recruitmentState);
    }
}
