using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Mainframe;

/// <summary>
/// Owns the mainframe shell window. Carries no state of its own — every tab is driven by its own BUI.
/// </summary>
public sealed class MainframeBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private MainframeUiController? _mainframe;

    public MainframeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _mainframe = _uiManager.GetUIController<MainframeUiController>();

        // Each faction's mainframe is its own prototype, so the entity name is already the machine's
        // identity — no extra faction field to drift out of sync with OverwatchConsole/PaymentConsole.
        var name = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _mainframe.RegisterShell(Owner, this, name);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _mainframe?.UnregisterShell(Owner);
    }
}
