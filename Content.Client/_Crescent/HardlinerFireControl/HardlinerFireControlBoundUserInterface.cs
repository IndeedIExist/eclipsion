// Ratgore Start
using Content.Shared._Crescent.HardlinerFireControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Crescent.HardlinerFireControl;

[UsedImplicitly]
public sealed class HardlinerFireControlBoundUserInterface : BoundUserInterface
{
    private HardlinerFireControlMenu? _menu;

    public HardlinerFireControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<HardlinerFireControlMenu>();
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not HardlinerFireControlConsoleBoundUserInterfaceState cast)
            return;

        _menu.ApplyTelemetry(cast.Rows);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (_menu == null || message is not HardlinerFireControlConsoleBoundUserInterfaceMessage cast)
            return;

        _menu.ApplyTelemetry(cast.Rows);
    }
}
// Ratgore End
