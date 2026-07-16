namespace Content.Client._Crescent.Mainframe;

/// <summary>
/// Tabs the mainframe window can host, in display order.
/// </summary>
/// <remarks>
/// Ordinals are the tab positions. The window pre-creates a slot per value at construction, so tab
/// order never depends on the order the sub-BUIs happen to be built in.
/// </remarks>
public enum MainframeTab : byte
{
    Overwatch = 0,
    Diplomacy = 1,
    Alert = 2,
    Economy = 3,
    Payment = 4,
}
