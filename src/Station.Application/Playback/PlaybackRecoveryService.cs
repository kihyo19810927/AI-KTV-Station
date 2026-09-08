using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Playback;

public enum PlaybackRecoveryAction { RetryCurrent, SkipCurrent, HaltPlayback }
public enum PlaybackFailureStage { Start, Load, Playback, Control }

public sealed record PlaybackRecoveryDecision(
    PlaybackRecoveryAction Action,
    TimeSpan Delay,
    bool RestartPlayer,
    int CompletedRetries,
    string PublicMessage);

public interface IPlaybackFailureStore
{
    Task RecordAsync(PlaybackError error, AvailabilityStatus? mediaAvailability, CancellationToken cancellationToken = default);
}

public sealed class PlaybackRecoveryPolicy(int maximumRetries = 2, TimeSpan? baseDelay = null, TimeSpan? maximumDelay = null)
{
    private readonly int maximumRetries = maximumRetries >= 0
        ? maximumRetries
        : throw new ArgumentOutOfRangeException(nameof(maximumRetries));
    private readonly TimeSpan baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(250);
    private readonly TimeSpan maximumDelay = maximumDelay ?? TimeSpan.FromSeconds(2);

    public PlaybackRecoveryDecision Decide(PlayerFailure failure, int completedRetries)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (completedRetries < 0) throw new ArgumentOutOfRangeException(nameof(completedRetries));

        if (failure.Kind is PlayerFailureKind.ExecutableMissing or PlayerFailureKind.StartFailed or
            PlayerFailureKind.InvalidState or PlayerFailureKind.InvalidArgument)
            return new(PlaybackRecoveryAction.HaltPlayback, TimeSpan.Zero, false, completedRetries,
                "Playback requires host attention before it can continue.");

        if (!failure.IsRetryable || failure.Kind == PlayerFailureKind.Unsupported)
            return new(PlaybackRecoveryAction.SkipCurrent, TimeSpan.Zero, false, completedRetries,
                "This song cannot be played and will be skipped.");

        if (completedRetries >= maximumRetries)
            return new(PlaybackRecoveryAction.SkipCurrent, TimeSpan.Zero, false, completedRetries,
                "Playback did not recover and this song will be skipped.");

        var multiplier = Math.Pow(2, completedRetries);
        var delay = TimeSpan.FromMilliseconds(Math.Min(maximumDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * multiplier));
        var restart = failure.Kind is PlayerFailureKind.ProcessExited or PlayerFailureKind.ConnectionTimeout or
            PlayerFailureKind.CommandTimeout or PlayerFailureKind.ProtocolError;
        return new(PlaybackRecoveryAction.RetryCurrent, delay, restart, completedRetries,
            "Playback was interrupted and will be retried.");
    }
}

public sealed class PlaybackRecoveryService(IPlaybackFailureStore store, PlaybackRecoveryPolicy policy)
{
    public async Task<Result<PlaybackRecoveryDecision>> DecideAndRecordAsync(
        Guid? mediaFileId,
        PlaybackFailureStage stage,
        PlayerFailure failure,
        int completedRetries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        PlaybackRecoveryDecision decision;
        try { decision = policy.Decide(failure, completedRetries); }
        catch (ArgumentOutOfRangeException)
        {
            return Result<PlaybackRecoveryDecision>.Failure(new Error("playback_recovery.invalid_retry_count", "Retry count cannot be negative."));
        }

        var error = new PlaybackError
        {
            MediaFileId = mediaFileId,
            ErrorCode = failure.Code,
            Stage = stage.ToString(),
            IsRetryable = failure.IsRetryable,
            DiagnosticSummary = $"{failure.Kind}:{decision.Action}",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        await store.RecordAsync(error, AvailabilityImpact(failure.Kind), cancellationToken).ConfigureAwait(false);
        return Result<PlaybackRecoveryDecision>.Success(decision);
    }

    private static AvailabilityStatus? AvailabilityImpact(PlayerFailureKind kind) => kind switch
    {
        PlayerFailureKind.MediaUnavailable => AvailabilityStatus.Offline,
        PlayerFailureKind.MediaLoadFailed or PlayerFailureKind.Unsupported => AvailabilityStatus.Unreadable,
        _ => null,
    };
}
