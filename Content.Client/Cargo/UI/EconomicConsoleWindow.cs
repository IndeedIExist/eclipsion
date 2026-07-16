using System.Numerics;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Cargo.UI;

/// <summary>
/// Standalone window for the economic console. Hosts the panel the BUI owns; on a mainframe the
/// same panel is hosted in a tab instead.
/// </summary>
public sealed class EconomicConsoleWindow : DefaultWindow
{
    public EconomicConsoleWindow()
    {
        Title = Loc.GetString("economic-console-title");
        MinSize = new Vector2(550, 450);
    }

    public void SetPanel(EconomicConsolePanel panel)
    {
        Contents.AddChild(panel);
    }
}
