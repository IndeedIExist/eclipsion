using Content.Server.Administration;
using Content.Shared._Crescent.Diplomacy;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Crescent.Diplomacy;

/// <summary>
/// Admin control over diplomacy that has carried between rounds. Both relation tables now persist, so a
/// sector left in a state nobody can talk their way out of — a war locked in by a round that ended badly —
/// would otherwise stay that way forever. This is the way out.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class ResetDiplomacyCommand : IConsoleCommand
{
    public string Command => "resetdiplomacy";
    public string Description => "Clears diplomacy carried over from previous rounds and restores the starting relations.";

    public string Help =>
        "Usage: resetdiplomacy <list|factions|iff|all>\n" +
        "  list     - show the relations currently in force\n" +
        "  factions - reset console diplomacy (war/peace/alliance/trade between factions)\n" +
        "  iff      - reset IFF relations back to their prototype defaults\n" +
        "  all      - both of the above";

    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var factions = _systems.GetEntitySystem<RatDiplomacySystem>();
        var iff = _systems.GetEntitySystem<DiplomacySystem>();

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                ListRelations(shell, factions);
                return;

            case "factions":
                factions.ResetRelations();
                shell.WriteLine("Faction diplomacy reset to starting relations.");
                return;

            case "iff":
                iff.ResetOverrides();
                shell.WriteLine("IFF relations reset to prototype defaults.");
                return;

            case "all":
                factions.ResetRelations();
                iff.ResetOverrides();
                shell.WriteLine("Faction diplomacy and IFF relations reset.");
                return;

            default:
                shell.WriteLine(Help);
                return;
        }
    }

    /// <summary>
    /// Only the pairs that are actually something get printed — a full matrix is mostly Neutral and buries
    /// the two lines an admin is looking for.
    /// </summary>
    private static void ListRelations(IConsoleShell shell, RatDiplomacySystem factions)
    {
        var seen = new HashSet<string>();
        var any = false;

        foreach (var (faction, relations) in factions.Relations)
        {
            foreach (var (other, relation) in relations)
            {
                if (relation == FactionRelation.Neutral)
                    continue;

                var key = string.CompareOrdinal(faction, other) <= 0 ? $"{faction}|{other}" : $"{other}|{faction}";
                if (!seen.Add(key))
                    continue;

                shell.WriteLine($"{faction,-6} {other,-6} {relation}");
                any = true;
            }
        }

        if (!any)
            shell.WriteLine("Every faction is Neutral toward every other.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                new[] { "list", "factions", "iff", "all" },
                "what to reset");
        }

        return CompletionResult.Empty;
    }
}
