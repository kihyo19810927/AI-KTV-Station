using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Playback;

public sealed record PlaybackProgress(
    Guid? PlaybackId,
    PlayerLifecycleState State,
    TimeSpan Position,
    TimeSpan? Duration);

public sealed class PlaybackControlService(IPlayerAdapter player)
{
    public async Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.ValidateVolume(volume);
        return validation is null
            ? await player.SetVolumeAsync(volume, cancellationToken).ConfigureAwait(false)
            : Result<PlayerSnapshot>.Failure(validation);
    }

    public async Task<Result<PlaybackProgress>> GetProgressAsync(CancellationToken cancellationToken = default)
    {
        var state = await player.GetStateAsync(cancellationToken).ConfigureAwait(false);
        return state.IsFailure
            ? Result<PlaybackProgress>.Failure(state.Error)
            : Result<PlaybackProgress>.Success(ToProgress(state.Value));
    }

    public async Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.ValidatePosition(position);
        if (validation is not null) return Result<PlayerSnapshot>.Failure(validation);
        var state = await RequireActivePlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (state.IsFailure) return state;
        if (state.Value.Duration is { } duration && position > duration)
            return Result<PlayerSnapshot>.Failure(new Error("player.position_out_of_range", "Playback position exceeds the media duration."));
        return await player.SeekAsync(position, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlayerSnapshot>> SelectSubtitleAsync(int? streamId, CancellationToken cancellationToken = default)
    {
        if (streamId is { } id && PlayerCommandValidation.ValidateTrack(id) is { } validation)
            return Result<PlayerSnapshot>.Failure(validation);
        var state = await RequireActivePlaybackAsync(cancellationToken).ConfigureAwait(false);
        if (state.IsFailure) return state;
        if (streamId is { } selectedId && !state.Value.Tracks.Any(x => x.StreamId == selectedId && x.Type == MediaTrackType.Subtitle))
            return Result<PlayerSnapshot>.Failure(new Error("player.subtitle_track_not_found", "The requested subtitle track is unavailable."));
        return await player.SelectSubtitleTrackAsync(streamId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PlayerSnapshot>> RequireActivePlaybackAsync(CancellationToken cancellationToken)
    {
        var state = await player.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state.IsFailure) return state;
        return state.Value.PlaybackId is not null && state.Value.State is PlayerLifecycleState.Playing or PlayerLifecycleState.Paused
            ? state
            : Result<PlayerSnapshot>.Failure(new Error("player.not_playing", "No active playback can accept this control."));
    }

    private static PlaybackProgress ToProgress(PlayerSnapshot state) =>
        new(state.PlaybackId, state.State, state.Position, state.Duration);
}
