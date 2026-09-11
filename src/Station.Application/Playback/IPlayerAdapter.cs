using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Playback;

public interface IPlayerAdapter : IAsyncDisposable
{
    Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> SkipAsync(CancellationToken cancellationToken = default) => StopAsync(cancellationToken);
    Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default);
    Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<PlayerEvent> WatchEventsAsync(CancellationToken cancellationToken = default);
}

public sealed record PlayerLoadRequest(Guid PlaybackId, string MediaPath);

public enum PlayerLifecycleState { Stopped, Idle, Preparing, Playing, Paused, Ended, Failed }

public sealed record PlayerSnapshot(
    PlayerLifecycleState State,
    Guid? PlaybackId,
    TimeSpan Position,
    TimeSpan? Duration,
    double Volume,
    int? AudioTrackId,
    int? SubtitleTrackId,
    IReadOnlyList<PlayerTrack> Tracks,
    PlayerFailure? Failure = null);

public sealed record PlayerTrack(int StreamId, MediaTrackType Type, string? Codec, string? Language, string? Title, bool IsSelected);

public enum PlayerFailureKind
{
    InvalidState,
    InvalidArgument,
    ExecutableMissing,
    StartFailed,
    ConnectionTimeout,
    CommandTimeout,
    ProcessExited,
    ProtocolError,
    MediaUnavailable,
    MediaLoadFailed,
    Unsupported,
    Unknown,
}

public sealed record PlayerFailure(string Code, PlayerFailureKind Kind, bool IsRetryable, string PublicMessage);

public enum PlaybackEndReason { Completed, Stopped, Replaced, Failed }

public abstract record PlayerEvent(Guid EventId, DateTimeOffset OccurredAt, Guid? PlaybackId);
public sealed record PlayerStartedEvent(Guid EventId, DateTimeOffset OccurredAt) : PlayerEvent(EventId, OccurredAt, null);
public sealed record PlayerStateChangedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid? PlaybackId, PlayerLifecycleState Previous, PlayerLifecycleState Current) : PlayerEvent(EventId, OccurredAt, PlaybackId);
public sealed record PlaybackStartedEvent : PlayerEvent
{
    public PlaybackStartedEvent(Guid eventId, DateTimeOffset occurredAt, Guid playbackId) : base(eventId, occurredAt, playbackId) { }
}
public sealed record PlaybackPausedEvent : PlayerEvent
{
    public PlaybackPausedEvent(Guid eventId, DateTimeOffset occurredAt, Guid playbackId) : base(eventId, occurredAt, playbackId) { }
}
public sealed record PlaybackEndedEvent : PlayerEvent
{
    public PlaybackEndedEvent(Guid eventId, DateTimeOffset occurredAt, Guid playbackId, PlaybackEndReason reason) : base(eventId, occurredAt, playbackId) => Reason = reason;
    public PlaybackEndReason Reason { get; }
}
public sealed record PlaybackFailedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid? PlaybackId, PlayerFailure Failure) : PlayerEvent(EventId, OccurredAt, PlaybackId);
public sealed record PlayerTracksChangedEvent : PlayerEvent
{
    public PlayerTracksChangedEvent(Guid eventId, DateTimeOffset occurredAt, Guid playbackId, IReadOnlyList<PlayerTrack> tracks) : base(eventId, occurredAt, playbackId) => Tracks = tracks;
    public IReadOnlyList<PlayerTrack> Tracks { get; }
}

public static class PlayerCommandValidation
{
    public static Error? Validate(PlayerLoadRequest request)
    {
        if (request.PlaybackId == Guid.Empty) return new Error("player.invalid_playback_id", "Playback id is required.");
        if (string.IsNullOrWhiteSpace(request.MediaPath)) return new Error("player.invalid_media_path", "Media path is required.");
        return null;
    }

    public static Error? ValidateVolume(double volume) => double.IsFinite(volume) && volume is >= 0 and <= 100
        ? null
        : new Error("player.invalid_volume", "Volume must be between zero and one hundred.");

    public static Error? ValidatePosition(TimeSpan position) => position >= TimeSpan.Zero
        ? null
        : new Error("player.invalid_position", "Playback position cannot be negative.");

    public static Error? ValidateTrack(int streamId) => streamId >= 0
        ? null
        : new Error("player.invalid_track", "Track id cannot be negative.");
}
