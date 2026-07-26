using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Crescent.Commands;

/// <summary>
/// Lists the commands this fork adds, grouped by what they are for.
///
/// Vanilla <c>help</c> lists every command the engine and content have between them, several hundred of them,
/// with nothing marking which handful are ours. Admins were finding these by word of mouth. The names below are
/// curated; the descriptions are read back out of the console host, so they cannot drift from the commands.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class CrescentHelpCommand : IConsoleCommand
{
    public string Command => "crescenthelp";
    public string Description => "Lists the Crescent-specific admin commands.";
    public string Help => "Usage: crescenthelp [search term]";

    [Dependency] private readonly IConsoleHost _conHost = default!;

    /// <summary>
    /// Add new fork commands here. A name with no registered command behind it is skipped rather than
    /// printed as a dead entry, so removing a command does not leave a lie in the list.
    /// </summary>
    private static readonly (string Category, string[] Commands)[] Categories =
    {
        ("Diplomacy", new[]
        {
            "resetdiplomacy",
            "getfactionrelations",
            "changefactionrelations",
            "changeifffaction",
        }),
        ("Economy", new[]
        {
            "stockmarket",
        }),
        ("Ships", new[]
        {
            "shieldentity",
            "unshieldentity",
            "pc_genranges",
        }),
        ("Round & objectives", new[]
        {
            "unionfall_skipgrace",
            "planetfall_releasebarrier",
        }),
        ("World", new[]
        {
            "sb_genchunks",
        }),
        ("Atmosphere & flavour", new[]
        {
            "adminvoice",
            "gridmusic",
            "gridflash",
            "dnadb",
        }),
    };

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var filter = args.Length > 0 ? args[0] : null;
        var found = false;

        foreach (var (category, commands) in Categories)
        {
            var matches = commands
                .Where(c => _conHost.AvailableCommands.ContainsKey(c))
                .Where(c => filter == null
                            || c.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            || _conHost.AvailableCommands[c].Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                continue;

            shell.WriteLine($"== {category} ==");
            foreach (var name in matches)
            {
                shell.WriteLine($"  {name,-26} {_conHost.AvailableCommands[name].Description}");
                found = true;
            }
        }

        if (!found)
        {
            shell.WriteLine(filter == null
                ? "No Crescent commands are registered."
                : $"No Crescent command matches '{filter}'.");
            return;
        }

        shell.WriteLine("");
        shell.WriteLine("Use 'help <command>' for full usage of any of these.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                Categories.SelectMany(c => c.Commands).Where(_conHost.AvailableCommands.ContainsKey),
                "search term");
        }

        return CompletionResult.Empty;
    }
}
