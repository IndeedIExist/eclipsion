using Content.Client.UserInterface.Fragments;
using Content.Shared._Crescent.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.CartridgeLoader.Cartridges;

public sealed partial class MoneyTransferUi : UIFragment
{
    private MoneyTransferUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new MoneyTransferUiFragment();
        _fragment.OnTransfer += (recipient, amount, comment) => Send(userInterface, recipient, amount, comment);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MoneyTransferUiState s)
            return;

        _fragment?.UpdateState(s);
    }

    private static void Send(BoundUserInterface bui, NetEntity recipient, int amount, string comment)
    {
        var ev = new MoneyTransferUiMessageEvent(recipient, amount, comment);
        bui.SendMessage(new CartridgeUiMessage(ev));
    }
}
