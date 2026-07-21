using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Server._Crescent.UnionfallCapturePoint;

namespace Content.Server._Crescent.UnionfallCapturePoint;

[AdminCommand(AdminFlags.Fun)]
public sealed class UnionfallSkipGraceCommand : IConsoleCommand
{
    public string Command => "unionfall_skipgrace";
    public string Description => "Ends the Unionfall grace period immediately and starts the war.";
    public string Help => "Usage: unionfall_skipgrace";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var announcerSystem = entityManager.System<UnionfallAnnouncerSystem>();
        announcerSystem.SkipGracePeriod();

        int capturePoints = 0;
        var cpQuery = entityManager.EntityQueryEnumerator<UnionfallCapturePointComponent>();
        while (cpQuery.MoveNext(out _, out var cp))
        {
            cp.GracePeriod = 0f;
            capturePoints++;
        }

        int shipNodes = 0;
        var snQuery = entityManager.EntityQueryEnumerator<UnionfallShipNodeComponent>();
        while (snQuery.MoveNext(out _, out var sn))
        {
            sn.GracePeriod = 0f;
            shipNodes++;
        }

        shell.WriteLine($"Unionfall grace period skipped. Activated {capturePoints} capture points and {shipNodes} ship nodes.");
    }
}