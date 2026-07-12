using Robust.Shared.Audio.Systems;

namespace Content.Shared._Crescent.Audio.CustomBoombox;

public abstract class SharedCustomBoomboxSystem : EntitySystem
{
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
}
