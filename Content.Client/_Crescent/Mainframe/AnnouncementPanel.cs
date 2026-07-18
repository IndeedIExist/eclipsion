using System;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client._Crescent.Mainframe;

/// <summary>
/// Compact station-announcement panel hosted as a mainframe tab. It only carries the message box and
/// send button — the announcement itself is dispatched through the standalone communications console's
/// own message and server system, so permissions, cooldown and formatting are identical.
/// </summary>
public sealed class AnnouncementPanel : BoxContainer
{
    private readonly TextEdit _messageInput;
    private readonly MonotoneButton _announceButton;
    private readonly int _maxLength;

    /// <summary>Cooldown gate from the server; combined with the length check to enable the button.</summary>
    private bool _canAnnounce = true;

    /// <summary>Raised with the typed announcement text when the player presses the send button.</summary>
    public event Action<string>? OnAnnounce;

    public AnnouncementPanel()
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var loc = IoCManager.Resolve<ILocalizationManager>();
        _maxLength = cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);

        Orientation = LayoutOrientation.Vertical;
        Margin = new Thickness(6);
        HorizontalExpand = true;
        VerticalExpand = true;

        _messageInput = new TextEdit
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinHeight = 120,
            Placeholder = new Rope.Leaf(loc.GetString("mainframe-announce-placeholder")),
        };
        _messageInput.OnTextChanged += _ => UpdateButtonState();

        _announceButton = new MonotoneButton
        {
            Text = loc.GetString("mainframe-announce-button"),
            HorizontalAlignment = HAlignment.Right,
            MinWidth = 160,
            Margin = new Thickness(0, 6, 0, 0),
        };
        _announceButton.OnPressed += _ =>
        {
            if (_announceButton.Disabled)
                return;

            OnAnnounce?.Invoke(Rope.Collapse(_messageInput.TextRope));
            _messageInput.TextRope = new Rope.Leaf("");
            UpdateButtonState();
        };

        AddChild(_messageInput);
        AddChild(_announceButton);

        UpdateButtonState();
    }

    /// <summary>Reflects the console's announcement cooldown; the button stays disabled until it clears.</summary>
    public void SetCanAnnounce(bool canAnnounce)
    {
        _canAnnounce = canAnnounce;
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        var tooLong = _messageInput.TextLength > _maxLength;
        _announceButton.Disabled = !_canAnnounce || tooLong;
        _announceButton.ToolTip = tooLong
            ? Loc.GetString("comms-console-message-too-long")
            : null;
    }
}
