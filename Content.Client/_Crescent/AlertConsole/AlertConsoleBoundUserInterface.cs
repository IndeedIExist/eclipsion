using System;
using Content.Shared._Crescent.AlertConsole;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.AlertConsole;

public sealed class AlertConsoleBoundUserInterface : BoundUserInterface
{
    private AlertConsoleMenu? _menu;

    public AlertConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<AlertConsoleMenu>();

        _menu.OnSavePressed += settings =>
        {
            SendMessage(new AlertConsoleSaveSettingsMessage(
                settings.Enabled,
                settings.DetectionRadius,
                settings.StationAlertMessage,
                settings.BroadcastToShuttle,
                settings.ShuttleAlertMessage,
                settings.AlertCooldownSeconds));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AlertConsoleBuiState buiState || _menu == null)
            return;

        _menu.UpdateState(buiState);
    }
}
