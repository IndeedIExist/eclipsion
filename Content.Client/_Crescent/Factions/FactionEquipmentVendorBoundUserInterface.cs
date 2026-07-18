using Content.Shared._Crescent.Factions;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.Factions;

public sealed class FactionEquipmentVendorBoundUserInterface : BoundUserInterface
{
    private FactionEquipmentVendorWindow? _window;

    public FactionEquipmentVendorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FactionEquipmentVendorWindow>();
        _window.OnTake += itemId => SendMessage(new FactionVendorTakeMessage(itemId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FactionVendorState vendorState)
            _window?.SetItems(vendorState.Items);
    }
}
