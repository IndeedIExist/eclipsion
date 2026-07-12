using Content.Shared._Crescent.LifeInsurance;
using Robust.Client.GameObjects;

namespace Content.Client._Crescent.LifeInsurance;

public sealed class LifeInsuranceBoundUserInterface : BoundUserInterface
{
    private LifeInsuranceWindow? _window;

    public LifeInsuranceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new LifeInsuranceWindow();
        _window.OnClose += Close;
        _window.InsurePressed += OnInsurePressed;
        _window.VoidPressed += OnVoidPressed;
        _window.EjectPressed += OnEjectPressed;
        _window.SpawnMachineSelected += OnSpawnMachineSelected;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
        {
            _window.InsurePressed -= OnInsurePressed;
            _window.VoidPressed -= OnVoidPressed;
            _window.EjectPressed -= OnEjectPressed;
            _window.SpawnMachineSelected -= OnSpawnMachineSelected;
            _window.Dispose();
        }
    }

    private void OnInsurePressed(NetEntity target, NetEntity spawnMachine)
    {
        SendMessage(new LifeInsuranceSelectTargetMessage(target, spawnMachine));
    }

    private void OnVoidPressed(NetEntity target)
    {
        SendMessage(new LifeInsuranceVoidInsuranceMessage(target));
    }

    private void OnEjectPressed()
    {
        SendMessage(new LifeInsuranceEjectProteinsMessage());
    }

    private void OnSpawnMachineSelected(NetEntity target, NetEntity spawnMachine)
    {
        SendMessage(new LifeInsuranceSetSpawnMachineMessage(target, spawnMachine));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not LifeInsuranceConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }
}
