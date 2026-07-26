using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Crescent.SignalTransmitter;

[RegisterComponent]
public sealed partial class SignalTransmitterDeviceComponent : Component
{
    /// <summary>
    /// Whether the transmitter has already been activated.
    /// </summary>
    [DataField]
    public bool Activated;

    /// <summary>
    /// Total countdown duration in minutes.
    /// </summary>
    [DataField]
    public int TimerMinutes = 10;

    /// <summary>
    /// Reminder time in minutes remaining.
    /// </summary>
    [DataField]
    public int ReminderMinutes = 5;

    /// <summary>
    /// Length of the activation code.
    /// </summary>
    [DataField]
    public int CodeLength = 6;

    /// <summary>
    /// The correct activation code (generated on MapInit).
    /// </summary>
    [ViewVariables]
    public string Code = string.Empty;

    /// <summary>
    /// Code entered so far by the user.
    /// </summary>
    [ViewVariables]
    public string EnteredCode = string.Empty;
}

[NetSerializable, Serializable]
public enum SignalTransmitterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SignalTransmitterUiState : BoundUserInterfaceState
{
    public bool Activated;
    public int EnteredCodeLength;
    public int MaxCodeLength;
    public bool IsAnchored;

    public SignalTransmitterUiState(bool activated, int enteredCodeLength, int maxCodeLength, bool isAnchored)
    {
        Activated = activated;
        EnteredCodeLength = enteredCodeLength;
        MaxCodeLength = maxCodeLength;
        IsAnchored = isAnchored;
    }
}

[Serializable, NetSerializable]
public sealed class TransmitterKeypadMessage : BoundUserInterfaceMessage
{
    public int Value;

    public TransmitterKeypadMessage(int value)
    {
        Value = value;
    }
}

[Serializable, NetSerializable]
public sealed class TransmitterKeypadClearMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TransmitterKeypadEnterMessage : BoundUserInterfaceMessage;
