using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Crescent.Audio.CustomBoombox;

[Serializable, NetSerializable]
public enum CustomBoomboxUiKey : byte
{
    Key,
}

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedCustomBoomboxSystem))]
public sealed partial class CustomBoomboxComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;

    [DataField, AutoNetworkedField]
    public string? SelectedTrackDisplayName;

    [DataField, AutoNetworkedField]
    public string? SelectedTrackResourcePath;

    [DataField, AutoNetworkedField]
    public float Volume = 100f;

    [DataField]
    public int UploadRevision;

    [ViewVariables]
    public bool AutoPlayAfterSelect;

    [DataField]
    public string? OnState;

    [DataField]
    public string? OffState;

    [DataField]
    public string? SelectState;

    [ViewVariables]
    public bool Selecting;

    [ViewVariables]
    public float SelectAccumulator;
}

[Serializable, NetSerializable]
public sealed class CustomBoomboxPlayingMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CustomBoomboxPauseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CustomBoomboxStopMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CustomBoomboxSetTimeMessage(float songTime) : BoundUserInterfaceMessage
{
    public float SongTime { get; } = songTime;
}

[Serializable, NetSerializable]
public sealed class CustomBoomboxSetVolumeMessage(float volume) : BoundUserInterfaceMessage
{
    public float Volume { get; } = volume;
}

[Serializable, NetSerializable]
public sealed class CustomBoomboxClearMessage : BoundUserInterfaceMessage;

