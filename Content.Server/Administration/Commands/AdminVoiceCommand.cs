using System.Linq;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class AdminVoiceCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ResPath VoiceDir = new ResPath("/Audio/_Crescent/Admin Voices");

    public string Command => "adminvoice";
    public string Description => "Plays an admin voice line for all players or a specific player.";
    public string Help => "Usage: adminvoice <filename> [username1] [username2] ...\nOmit username(s) to play for everyone.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            ListVoices(shell);
            return;
        }

        var filename = args[0];
        if (!filename.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            filename += ".ogg";

        var path = VoiceDir / filename;

        if (!VoiceExists(path))
        {
            shell.WriteError($"Voice file not found: {path}\nUse 'adminvoice' with no arguments to list available voices.");
            return;
        }

        Filter filter;

        if (args.Length == 1)
        {
            filter = Filter.Empty().AddAllPlayers(_playerManager);
        }
        else
        {
            filter = Filter.Empty();
            for (var i = 1; i < args.Length; i++)
            {
                if (!_playerManager.TryGetSessionByUsername(args[i], out var session))
                {
                    shell.WriteError($"Player '{args[i]}' not found.");
                    continue;
                }
                filter.AddPlayer(session);
            }
        }

        var audio = AudioParams.Default.AddVolume(-8);
        _entManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(filter, path.ToString(), audio, replay: args.Length == 1);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var voices = GetVoiceFiles();
            return CompletionResult.FromHintOptions(voices, "voice filename (tab to list)");
        }

        var players = _playerManager.Sessions.Select<ICommonSession, string>(s => s.Name);
        return CompletionResult.FromHintOptions(players, $"player {args.Length - 1} (optional)");
    }

    private void ListVoices(IConsoleShell shell)
    {
        var voices = GetVoiceFiles().ToList();
        if (voices.Count == 0)
        {
            shell.WriteLine("No voice files found.");
            return;
        }
        shell.WriteLine("Available voices:");
        foreach (var v in voices)
            shell.WriteLine($"  {v}");
    }

    /// <summary>
    /// Packaged servers have all of Audio/ stripped out, so the files only exist as audioMetadata
    /// prototypes there. Check both so the command works in dev and on a real server.
    /// </summary>
    private bool VoiceExists(ResPath path)
    {
        return _res.ContentFileExists(path) || _proto.HasIndex<AudioMetadataPrototype>(path.ToString());
    }

    private IEnumerable<string> GetVoiceFiles()
    {
        var prefix = VoiceDir.ToString() + "/";

        var fromDisk = _res.ContentFindFiles(VoiceDir)
            .Where(p => p.Extension == "ogg")
            .Select(p => p.Filename);

        var fromMetadata = _proto.EnumeratePrototypes<AudioMetadataPrototype>()
            .Select(p => p.ID)
            .Where(id => id.StartsWith(prefix, StringComparison.Ordinal) && id.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            .Select(id => id[prefix.Length..]);

        return fromDisk.Union(fromMetadata).OrderBy(f => f);
    }
}
