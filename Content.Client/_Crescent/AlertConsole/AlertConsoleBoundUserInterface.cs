using System;
using Content.Client._Crescent.Mainframe;
using Content.Shared._Crescent.AlertConsole;
using Content.Shared._Crescent.Mainframe;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.AlertConsole;

public sealed class AlertConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private AlertConsoleMenu? _menu;
    private AlertPanel? _panel;
    private MainframeUiController? _mainframe;

    public AlertConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _panel = new AlertPanel();
        _panel.OnSavePressed += OnSave;

        // On a mainframe this renders as a tab instead of its own window. Asking the UI system
        // rather than checking for a marker component avoids racing component state application.
        if (UiSystem.HasUi(Owner, MainframeUiKey.Key))
        {
            _mainframe = _uiManager.GetUIController<MainframeUiController>();
            _mainframe.AttachPanel(Owner, MainframeTab.Alert, _panel, this);
        }
        else
        {
            _menu = this.CreateWindow<AlertConsoleMenu>();
            _menu.SetPanel(_panel);
        }
    }

    private void OnSave(AlertConsoleBuiState settings)
    {
        SendMessage(new AlertConsoleSaveSettingsMessage(
            settings.Enabled,
            settings.DetectionRadius,
            settings.StationAlertMessage,
            settings.BroadcastToShuttle,
            settings.ShuttleAlertMessage,
            settings.AlertCooldownSeconds));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AlertConsoleBuiState buiState)
            _panel?.UpdateState(buiState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _mainframe?.DetachPanel(Owner, MainframeTab.Alert);
    }
}
