using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Crescent.Payment;

/// <summary>
/// Prompts for a one-off bonus amount and a reason. The reason is mandatory — bonuses are logged.
/// </summary>
public sealed class PaymentBonusDialog : DefaultWindow
{
    private readonly LineEdit _amountEdit;
    private readonly LineEdit _reasonEdit;
    private readonly Button _confirmButton;

    public event Action<int, string>? OnConfirmed;

    public PaymentBonusDialog(string memberName)
    {
        Title = Loc.GetString("payment-console-bonus-title", ("name", memberName));
        MinSize = new Vector2(360, 190);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            HorizontalExpand = true,
        };

        box.AddChild(new Label { Text = Loc.GetString("payment-console-bonus-amount-label") });
        _amountEdit = new LineEdit { HorizontalExpand = true, Margin = new Thickness(0, 2, 0, 6) };
        box.AddChild(_amountEdit);

        box.AddChild(new Label { Text = Loc.GetString("payment-console-bonus-reason-label") });
        _reasonEdit = new LineEdit { HorizontalExpand = true, Margin = new Thickness(0, 2, 0, 8) };
        box.AddChild(_reasonEdit);

        _confirmButton = new Button
        {
            Text = Loc.GetString("payment-console-bonus-confirm"),
            HorizontalAlignment = HAlignment.Right,
            Disabled = true,
        };
        _confirmButton.OnPressed += _ =>
        {
            if (!int.TryParse(_amountEdit.Text, out var amount) || amount <= 0)
                return;

            OnConfirmed?.Invoke(amount, _reasonEdit.Text.Trim());
            Close();
        };
        box.AddChild(_confirmButton);

        _amountEdit.OnTextChanged += _ => Revalidate();
        _reasonEdit.OnTextChanged += _ => Revalidate();

        Contents.AddChild(box);
    }

    private void Revalidate()
    {
        _confirmButton.Disabled =
            !int.TryParse(_amountEdit.Text, out var amount)
            || amount <= 0
            || string.IsNullOrWhiteSpace(_reasonEdit.Text);
    }
}
