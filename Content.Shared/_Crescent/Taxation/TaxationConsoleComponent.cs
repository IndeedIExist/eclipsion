namespace Content.Shared._Crescent.Taxation;

/// <summary>
/// Marks a console that configures the trade tax rates of its owning station's trade points.
/// The console cannot change base trade prices, only the percentage tax skimmed off each sale.
/// Editing requires passing the console's <c>AccessReader</c> (faction access); anyone may view.
/// </summary>
[RegisterComponent]
public sealed partial class TaxationConsoleComponent : Component
{
}
