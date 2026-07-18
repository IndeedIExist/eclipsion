using Content.Shared.Containers.ItemSlots;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Shipyard;

/// <summary>
/// Component for the shipyard console.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedShipyardConsoleSystem))]
public sealed partial class ShipyardConsoleComponent : Component
{
    /// <summary>
    /// Sound played when the ship can't be bought for any reason.
    /// </summary>
    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// Sound played when a ship is purchased.
    /// </summary>
    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// Radio channel to send the purchase announcement to.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Command";

    /// <summary>
    /// Whether ships can be sold at this console. Defaults to true so every existing shipyard keeps
    /// working; set to false on LPC-only / empty consoles to prevent selling (and money exploits).
    /// </summary>
    [DataField]
    public bool CanSell = true;

    /// Hullrot edit
    public static string TargetIdCardSlotId = "ShipyardConsole-targetId";

    [DataField("targetIdSlot")]
    public ItemSlot TargetIdSlot = new();


    [DataField("shipyardChannel")]
    public string ShipyardChannel = "CentCom";

    [DataField("securityShipyardChannel")]
    public string SecurityShipyardChannel = "CentCom";

    /// Hulllrot edit end

}
