using Content.Client._Crescent.Shuttles.UI;
using Content.Shared._Crescent.Shuttles.BUIStates;
using Content.Shared._Crescent.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Shuttles.BUI;

[UsedImplicitly]
public sealed class MassCloakConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MassCloakConsoleWindow? _window;

    public MassCloakConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MassCloakConsoleWindow>();
        _window.MassCloak += SendMassCloakMessage;
        _window.MassCloakRangeChanged += SendMassCloakMessage;
        _window.OpenCenteredLeft();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MassCloakConsoleBoundUserInterfaceState bState)
            return;

        _window?.UpdateState(bState);
    }

    private void SendMassCloakMessage(bool enabled)
    {
        // Keep a default until the server sends the attached state.
        SendMessage(new MassCloakSetMessage()
        {
            Enabled = enabled,
            Range = 20f,
        });
    }

    private void SendMassCloakMessage(float range)
    {
        SendMessage(new MassCloakSetMessage()
        {
            Enabled = true,
            Range = range,
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Close();
            _window = null;
        }
    }
}
