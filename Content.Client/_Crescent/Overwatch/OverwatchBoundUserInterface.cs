using Content.Shared._Crescent.Overwatch;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client._Crescent.Overwatch;

/// <summary>
/// BUI for the Overwatch console.
/// </summary>
public sealed class OverwatchBoundUserInterface : BoundUserInterface
{
    private OverwatchWindow? _window;

    public OverwatchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    /// <inheritdoc/>
    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<OverwatchWindow>();
        _window.Initialize(this);

        _window.OnClose += () =>
        {
            _window = null;
        };
    }

    /// <inheritdoc/>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is OverwatchUpdateState updateState)
        {
            _window?.UpdateState(updateState);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _window?.Dispose();
        _window = null;
        base.Dispose(disposing);
    }

    public void ViewCamera(NetEntity target)
    {
        SendMessage(new OverwatchViewCameraMessage(target));
    }

    public void StopWatching()
    {
        SendMessage(new OverwatchStopWatchingMessage());
    }

    public void SetStatusFilter(OverwatchMemberStatus? status)
    {
        SendMessage(new OverwatchSetStatusFilterMessage(status));
    }

    public void SetSquadFilter(int? squadId)
    {
        SendMessage(new OverwatchSetSquadFilterMessage(squadId));
    }

    public void SetSearchQuery(string query)
    {
        SendMessage(new OverwatchSetSearchMessage(query));
    }

    public void CreateSquad(string squadName)
    {
        SendMessage(new OverwatchCreateSquadMessage(squadName));
    }

    public void DeleteSquad(int squadId)
    {
        SendMessage(new OverwatchDeleteSquadMessage(squadId));
    }

    public void AssignSquad(NetEntity player, int squadId)
    {
        SendMessage(new OverwatchAssignSquadMessage(player, squadId));
    }

    public void RemoveSquadMember(NetEntity player)
    {
        SendMessage(new OverwatchRemoveSquadMemberMessage(player));
    }

    public void SendAnnouncement(string message, int? targetSquadId = null)
    {
        SendMessage(new OverwatchSendMessageAnnouncement(message, targetSquadId));
    }
}
