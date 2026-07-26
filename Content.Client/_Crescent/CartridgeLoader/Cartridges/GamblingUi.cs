using Content.Client.UserInterface.Fragments;
using Content.Shared._Crescent.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.CartridgeLoader.Cartridges;

public sealed partial class GamblingUi : UIFragment
{
    private GamblingUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new GamblingUiFragment();

        _fragment.OnSpinSlots += bet =>
            Send(userInterface, new GamblingSpinSlotsMessage(bet));

        _fragment.OnRouletteBet += (kind, number, bet) =>
            Send(userInterface, new GamblingRouletteBetMessage(kind, number, bet));

        _fragment.OnBlackjack += (action, bet) =>
            Send(userInterface, new GamblingBlackjackMessage(action, bet));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not GamblingUiState cast)
            return;

        _fragment?.UpdateState(cast);
    }

    private static void Send(BoundUserInterface bui, CartridgeMessageEvent ev)
    {
        bui.SendMessage(new CartridgeUiMessage(ev));
    }
}
