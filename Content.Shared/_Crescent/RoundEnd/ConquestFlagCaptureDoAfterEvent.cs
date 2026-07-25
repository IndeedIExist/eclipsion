using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.RoundEnd;

/// <summary>
/// Raised on a <see cref="ConquestFlagComponent"/> when someone finishes — or breaks off — hauling its banner down.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ConquestFlagCaptureDoAfterEvent : SimpleDoAfterEvent
{
}
