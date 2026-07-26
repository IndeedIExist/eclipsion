using Content.Client.Gameplay;
using Content.Shared._Crescent.Audio.CustomBoombox;
using Content.Shared.Audio.Jukebox;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Audio;

/// <summary>
/// Smoothly lowers ("ducks") the game's own ambient/combat music while the local
/// player is standing near a playing jukebox or boombox, so the boombox track can
/// be heard clearly. The closer you get, the more the game music is ducked, and it
/// eases back up smoothly once you walk away or the boombox stops.
/// </summary>
public sealed partial class ContentAudioSystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    // Distance (in tiles) at which ducking begins. At this range there is no duck,
    // ramping up to the full amount right next to the boombox.
    private const float DuckRange = 12f;

    // How much (in dB) to lower the game music by when standing on top of a boombox.
    private const float MaxDuckDb = -14f;

    // How fast (dB per second) the duck eases in and out. Lower = smoother/slower.
    private const float DuckChangeSpeed = 9f;

    // Current applied duck offset in dB (<= 0). Eased toward the target each frame.
    private float _currentDuckDb;

    // Whether we touched the music volume last frame, so we know to restore it once.
    private bool _wasDucking;

    private void UpdateDucking(float frameTime)
    {
        var target = GetDuckTarget();

        _currentDuckDb = MoveToward(_currentDuckDb, target, DuckChangeSpeed * frameTime);

        // Not ducking and nothing to restore: leave the music volume alone entirely
        // so the normal ambient/volume logic keeps ownership of it.
        if (MathF.Abs(_currentDuckDb) < 0.01f)
        {
            if (!_wasDucking)
                return;

            _currentDuckDb = 0f;
            ApplyDuck();
            _wasDucking = false;
            return;
        }

        _wasDucking = true;
        ApplyDuck();
    }

    /// <summary>
    /// Returns the strongest (most negative) duck offset warranted by any playing
    /// boombox near the local player, or 0 if none are in range.
    /// </summary>
    private float GetDuckTarget()
    {
        if (_state.CurrentState is not GameplayState)
            return 0f;

        if (_player.LocalEntity is not { } player || !Exists(player))
            return 0f;

        var playerCoords = _xform.GetMapCoordinates(player);
        var best = 0f;

        var jukeQuery = AllEntityQuery<JukeboxComponent>();
        while (jukeQuery.MoveNext(out var uid, out var juke))
        {
            best = MathF.Min(best, DuckFor(uid, juke.AudioStream, playerCoords));
        }

        var boomboxQuery = AllEntityQuery<CustomBoomboxComponent>();
        while (boomboxQuery.MoveNext(out var uid, out var boombox))
        {
            best = MathF.Min(best, DuckFor(uid, boombox.AudioStream, playerCoords));
        }

        return best;
    }

    /// <summary>
    /// Computes the duck offset (<= 0) contributed by a single source, scaled by how
    /// close the player is. Returns 0 if the source isn't playing or is out of range.
    /// </summary>
    private float DuckFor(EntityUid source, EntityUid? audioStream, MapCoordinates playerCoords)
    {
        if (!_audio.IsPlaying(audioStream))
            return 0f;

        var coords = _xform.GetMapCoordinates(source);
        if (coords.MapId != playerCoords.MapId)
            return 0f;

        var dist = (coords.Position - playerCoords.Position).Length();
        if (dist >= DuckRange)
            return 0f;

        // 1 right on top of the boombox, 0 at the edge of the range.
        var factor = 1f - dist / DuckRange;
        return MaxDuckDb * factor;
    }

    /// <summary>
    /// Applies the current duck offset on top of the ambient music's intended volume.
    /// </summary>
    private void ApplyDuck()
    {
        if (_ambientMusicStream is not { } stream || _musicProto == null)
            return;

        // The track is on its way out and will be stopped by the fade; leave it alone.
        // _musicProto may already describe the *next* track at this point, so the volume
        // we'd compute here wouldn't even belong to this stream.
        if (_fadingOut.ContainsKey(stream))
            return;

        if (!TryComp(stream, out AudioComponent? comp))
            return;

        var slider = _isCombatMusicPlaying ? _volumeSliderCombat : _volumeSliderAmbient;
        var target = GetDuckedVolume(_musicProto.Sound.Params.Volume + slider);

        // Mid fade-in (10s for ambient tracks) we don't set the volume directly, we retarget
        // the fade instead. Otherwise the duck would be ignored for the whole fade and then
        // slam the volume down by MaxDuckDb in a single frame the moment the fade finished.
        if (_fadingIn.TryGetValue(stream, out var fade))
        {
            _fadingIn[stream] = (fade.VolumeChange, target);
            return;
        }

        // Volume round-trips through gain, so compare with an (inaudible) tolerance rather
        // than exactly, otherwise we'd re-set the volume every single frame.
        if (MathHelper.CloseTo(comp.Volume, target, 0.01f))
            return;

        _audio.SetVolume(stream, target, comp);
    }

    /// <summary>
    /// Applies the current duck to an intended music volume, keeping the result above
    /// <see cref="MinVolume"/>. Going under that floor breaks <see cref="FadeOut"/>: it would
    /// compute a negative per-tick change, so the stream fades *up* instead of down, never
    /// reaches MinVolume, is never stopped, and keeps playing over the next track.
    /// </summary>
    private float GetDuckedVolume(float baseVolume)
    {
        var floor = MathF.Min(baseVolume, MinVolume + 0.5f);
        return MathF.Max(baseVolume + _currentDuckDb, floor);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;

        return current + MathF.Sign(target - current) * maxDelta;
    }
}
