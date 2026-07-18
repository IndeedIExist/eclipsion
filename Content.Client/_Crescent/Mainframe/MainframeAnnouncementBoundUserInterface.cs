using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Shared._Crescent.Mainframe;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Crescent.Mainframe;

/// <summary>
/// Drives the mainframe's announcement tab. This is the only sub-console the mainframe adds that isn't
/// backed by a Crescent console of its own — it reuses the vanilla communications console component and
/// server system, so an announcement made here is indistinguishable from one made at a standalone comms
/// console (same access check, cooldown and formatting). Only the message path is exposed; alert-level
/// and shuttle controls are deliberately left out.
/// </summary>
public sealed class MainframeAnnouncementBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AnnouncementPanel? _panel;
    private MainframeUiController? _mainframe;

    public MainframeAnnouncementBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _panel = new AnnouncementPanel();
        _panel.OnAnnounce += OnAnnounce;

        // This BUI is only ever mapped on a mainframe, but the guard keeps a stray standalone mapping
        // from NREing on the missing controller rather than silently doing nothing.
        if (UiSystem.HasUi(Owner, MainframeUiKey.Key))
        {
            _mainframe = _uiManager.GetUIController<MainframeUiController>();
            _mainframe.AttachPanel(Owner, MainframeTab.Announcement, _panel, this);
        }
    }

    private void OnAnnounce(string message)
    {
        // Sanitize client-side exactly as the standalone console does; the server sanitizes again.
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        SendMessage(new CommunicationsConsoleAnnounceMessage(msg));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CommunicationsConsoleInterfaceState commsState)
            _panel?.SetCanAnnounce(commsState.CanAnnounce);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _mainframe?.DetachPanel(Owner, MainframeTab.Announcement);
    }
}
