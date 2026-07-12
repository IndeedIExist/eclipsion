using Content.Shared._Crescent.Diplomacy;

namespace Content.Client._Crescent.Diplomacy;

public sealed class DiplomacyConsoleBoundUserInterface : BoundUserInterface
{
    private DiplomacyConsoleMenu? _menu;

    public DiplomacyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = new DiplomacyConsoleMenu();

        _menu.OnDeclareWar += targetFactionId =>
            SendMessage(new DiplomacyDeclareWarMessage(targetFactionId));

        _menu.OnProposePeace += targetFactionId =>
            SendMessage(new DiplomacyProposePeaceMessage(targetFactionId));

        _menu.OnProposeAlliance += targetFactionId =>
            SendMessage(new DiplomacyProposeAllianceMessage(targetFactionId));

        _menu.OnProposeTrade += targetFactionId =>
            SendMessage(new DiplomacyProposeTradeMessage(targetFactionId));

        _menu.OnBreakTrade += targetFactionId =>
            SendMessage(new DiplomacyBreakTradeMessage(targetFactionId));

        _menu.OnAcceptProposal += (fromFactionId, type) =>
            SendMessage(new DiplomacyAcceptProposalMessage(fromFactionId, type));

        _menu.OnRejectProposal += (fromFactionId, type) =>
            SendMessage(new DiplomacyRejectProposalMessage(fromFactionId, type));

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DiplomacyConsoleBoundUserInterfaceState uiState)
            _menu?.UpdateState(uiState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _menu?.Dispose();
    }
}
