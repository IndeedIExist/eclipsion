using Content.Client._Crescent.Mainframe;
using Content.Shared._Crescent.Mainframe;
using Content.Shared._Crescent.Payment;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Payment;

public sealed class PaymentConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private PaymentConsoleWindow? _window;
    private PaymentPanel? _panel;
    private MainframeUiController? _mainframe;

    public PaymentConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _panel = new PaymentPanel();

        _panel.OnSetSalary += (user, salary) => SendMessage(new PaymentSetSalaryMessage(user, salary));
        _panel.OnClearSalary += user => SendMessage(new PaymentClearSalaryMessage(user));
        _panel.OnBonus += (user, amount, reason) => SendMessage(new PaymentBonusMessage(user, amount, reason));

        // On a mainframe this renders as a tab instead of its own window.
        if (UiSystem.HasUi(Owner, MainframeUiKey.Key))
        {
            _mainframe = _uiManager.GetUIController<MainframeUiController>();
            _mainframe.AttachPanel(Owner, MainframeTab.Payment, _panel, this);
        }
        else
        {
            _window = this.CreateWindow<PaymentConsoleWindow>();
            _window.SetPanel(_panel);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PaymentConsoleState paymentState)
            _panel?.UpdateState(paymentState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _mainframe?.DetachPanel(Owner, MainframeTab.Payment);
    }
}
