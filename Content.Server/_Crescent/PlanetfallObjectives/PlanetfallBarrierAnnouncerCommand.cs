using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Crescent.PlanetfallObjectives;

[AdminCommand(AdminFlags.Debug)]
public sealed class PlanetfallBarrierAnnouncerCommand : IConsoleCommand
{
    public string Command => "planetfall_releasebarrier";
    public string Description => "Immediately releases the Planetfall barrier on your current map.";
    public string Help => "Run while attached to an entity on the Planetfall map.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } playerEntity)
        {
            shell.WriteError("You must be attached to an entity on the target map.");
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!entityManager.TryGetComponent<TransformComponent>(playerEntity, out var transform))
        {
            shell.WriteError("Attached entity has no transform.");
            return;
        }

        var released = entityManager.System<PlanetfallBarrierAnnouncerSystem>().TryReleaseOnMap(transform.MapID);
        if (!released)
        {
            shell.WriteError("No unreleased Planetfall barrier announcer was found on your current map.");
            return;
        }

        shell.WriteLine("Planetfall barrier release triggered.");
    }
}
