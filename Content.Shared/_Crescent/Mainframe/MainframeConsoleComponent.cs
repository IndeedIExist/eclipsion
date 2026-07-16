namespace Content.Shared._Crescent.Mainframe;

/// <summary>
/// Marks a console that hosts the other command UIs as tabs in one window.
/// </summary>
/// <remarks>
/// Only server systems read this. It lives in Shared because prototypes reference it by name,
/// and it is deliberately not networked — the client identifies a mainframe by asking whether the
/// entity has <see cref="MainframeUiKey.Key"/>, which is available before any component state applies.
/// </remarks>
[RegisterComponent]
public sealed partial class MainframeConsoleComponent : Component
{
}
