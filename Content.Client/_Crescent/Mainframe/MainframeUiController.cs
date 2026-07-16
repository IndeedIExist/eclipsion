using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Crescent.Mainframe;

/// <summary>
/// Owns the shared mainframe window for each console and collects the per-console panels into it.
/// </summary>
/// <remarks>
/// Each tab is still a real <see cref="BoundUserInterface"/> on its own UI key — this only redirects
/// where they render. The window is created lazily by whichever BUI arrives first, because BUI
/// construction order is unspecified, and torn down once the last one lets go.
/// </remarks>
public sealed class MainframeUiController : UIController
{
    private readonly Dictionary<EntityUid, Shell> _shells = new();

    private sealed record Tab(Control Panel, BoundUserInterface Bui);

    private sealed class Shell
    {
        public MainframeWindow Window = default!;
        public readonly Dictionary<MainframeTab, Tab> Panels = new();
        public MainframeBoundUserInterface? Owner;

        /// <summary>Set while a close is cascading, so the window isn't disposed out from under it.</summary>
        public bool Tearing;

        public int RefCount => Panels.Count + (Owner is null ? 0 : 1);
    }

    private Shell GetOrCreateShell(EntityUid uid)
    {
        if (_shells.TryGetValue(uid, out var existing))
            return existing;

        var shell = new Shell
        {
            Window = new MainframeWindow(),
        };

        // Not CreateWindow<T>(): that ties the window's lifetime to one BUI, and this window both
        // predates and outlives any individual one.
        shell.Window.OnClose += () => OnWindowClosed(uid);
        _shells[uid] = shell;
        shell.Window.OpenCentered();
        return shell;
    }

    public void RegisterShell(EntityUid uid, MainframeBoundUserInterface bui, string machineName)
    {
        var shell = GetOrCreateShell(uid);
        shell.Owner = bui;
        shell.Window.SetMachineName(machineName);
    }

    public void UnregisterShell(EntityUid uid)
    {
        if (!_shells.TryGetValue(uid, out var shell))
            return;

        shell.Owner = null;
        Release(uid, shell);
    }

    public void AttachPanel(EntityUid uid, MainframeTab tab, Control panel, BoundUserInterface bui)
    {
        var shell = GetOrCreateShell(uid);
        shell.Panels[tab] = new Tab(panel, bui);
        shell.Window.SetTab(tab, panel);
    }

    /// <summary>
    /// Shows or hides a shell window without tearing it down. Used by a hosted console (Overwatch)
    /// to clear the screen while the player spectates through a camera, then bring the shell back.
    /// </summary>
    public void SetShellVisible(EntityUid uid, bool visible)
    {
        if (_shells.TryGetValue(uid, out var shell))
            shell.Window.Visible = visible;
    }

    /// <summary>
    /// Drops a tab and disposes its panel. Panels are disposed here rather than by each BUI because
    /// only the standalone path gets CreateWindow's automatic disposal.
    /// </summary>
    public void DetachPanel(EntityUid uid, MainframeTab tab)
    {
        if (!_shells.TryGetValue(uid, out var shell))
            return;

        if (shell.Panels.Remove(tab, out var entry))
        {
            shell.Window.ClearTab(tab);
            entry.Panel.Dispose();
        }

        Release(uid, shell);
    }

    private void OnWindowClosed(EntityUid uid)
    {
        if (!_shells.TryGetValue(uid, out var shell) || shell.Tearing)
            return;

        shell.Tearing = true;

        // Teardown is server-driven: closing the mainframe key makes the server close every sub-key,
        // which disposes the sub-BUIs, which detach their panels back through DetachPanel. The
        // sub-BUIs are closed directly too, so a missing shell BUI can't strand them open.
        shell.Owner?.Close();
        foreach (var entry in shell.Panels.Values.ToArray())
            entry.Bui.Close();

        shell.Tearing = false;
        Release(uid, shell);
    }

    private void Release(EntityUid uid, Shell shell)
    {
        if (shell.Tearing || shell.RefCount > 0)
            return;

        _shells.Remove(uid);
        shell.Window.Dispose();
    }
}
