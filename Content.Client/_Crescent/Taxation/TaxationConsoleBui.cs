using Content.Shared._Crescent.Taxation;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Taxation;

public sealed class TaxationConsoleBui : BoundUserInterface
{
    private TaxationConsoleWindow? _window;

    public TaxationConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new TaxationConsoleWindow();
        _window.OnClose += Close;
        _window.OnSetDefault += rate => SendMessage(new TaxationSetDefaultRateMessage(rate));
        _window.OnSetOverride += tuple => SendMessage(new TaxationSetOverrideMessage(tuple.ProtoId, tuple.Rate));
        _window.OnClearOverride += protoId => SendMessage(new TaxationClearOverrideMessage(protoId));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is TaxationConsoleState cast)
            _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
