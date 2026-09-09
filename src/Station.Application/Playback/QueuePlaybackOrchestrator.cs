using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Playback;

public sealed record PlayableQueueItem(
    Guid QueueItemId,
    Guid RoomId,
    Guid SongId,
    Guid MediaFileId,
    string MediaPath,
    PlayerFailure? PreflightFailure = null);

public interface IPlaybackQueueStore
{
    Task<PlayableQueueItem?> GetNextAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task SetQueueStatusAsync(Guid queueItemId, QueueItemStatus status, DateTimeOffset? completedAt, CancellationToken cancellationToken = default);
    Task<PlayHistory> StartHistoryAsync(PlayableQueueItem item, DateTimeOffset startedAt, CancellationToken cancellationToken = default);
    Task CompleteHistoryAsync(Guid historyId, PlaybackOutcome outcome, DateTimeOffset endedAt, string? errorCode, CancellationToken cancellationToken = default);
}

public sealed record PlaybackOrchestrationState(
    Guid RoomId,
    Guid? QueueItemId,
    Guid? PlaybackId,
    QueueItemStatus? QueueStatus,
    bool IsRunning,
    bool IsHalted,
    int RetryCount,
    string? LastErrorCode);

public sealed class QueuePlaybackOrchestrator(
    IPlayerAdapter player,
    IPlaybackQueueStore store,
    PlaybackRecoveryService recovery,
    TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ActivePlayback? active;
    private Guid roomId;
    private bool running;
    private bool halted;
    private string? lastErrorCode;

    public PlaybackOrchestrationState Current => new(
        roomId,
        active?.Item.QueueItemId,
        active?.PlaybackId,
        active?.Status,
        running,
        halted,
        active?.RetryCount ?? 0,
        lastErrorCode);

    public async Task<Result<PlaybackOrchestrationState>> StartAsync(Guid targetRoomId, CancellationToken cancellationToken = default)
    {
        if (targetRoomId == Guid.Empty) return Failure("orchestration.invalid_room", "Room id is required.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (running && roomId != targetRoomId)
                return Failure("orchestration.already_running", "Playback is already running for another room.");
            roomId = targetRoomId;
            running = true;
            halted = false;
            lastErrorCode = null;
            var state = await player.GetStateAsync(cancellationToken).ConfigureAwait(false);
            if (state.IsFailure) return Failure(state.Error.Code, state.Error.Message);
            if (state.Value.State == PlayerLifecycleState.Stopped)
            {
                var started = await player.StartAsync(cancellationToken).ConfigureAwait(false);
                if (started.IsFailure) return Failure(started.Error.Code, started.Error.Message);
            }
            if (active is null) await StartNextCoreAsync(cancellationToken).ConfigureAwait(false);
            return Result<PlaybackOrchestrationState>.Success(Current);
        }
        finally { gate.Release(); }
    }

    public async Task RunAsync(Guid targetRoomId, CancellationToken cancellationToken = default)
    {
        var started = await StartAsync(targetRoomId, cancellationToken).ConfigureAwait(false);
        if (started.IsFailure) return;
        await foreach (var playerEvent in player.WatchEventsAsync(cancellationToken).ConfigureAwait(false))
            await HandleAsync(playerEvent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlaybackOrchestrationState>> HandleAsync(PlayerEvent playerEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerEvent);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!running || active is null || playerEvent.PlaybackId != active.PlaybackId)
                return Result<PlaybackOrchestrationState>.Success(Current);
            switch (playerEvent)
            {
                case PlaybackStartedEvent:
                    active.Status = QueueItemStatus.Playing;
                    await store.SetQueueStatusAsync(active.Item.QueueItemId, QueueItemStatus.Playing, null, cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackEndedEvent ended when ended.Reason == PlaybackEndReason.Completed:
                    await FinishCurrentAsync(QueueItemStatus.Completed, PlaybackOutcome.Completed, null, cancellationToken).ConfigureAwait(false);
                    await StartNextCoreAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackEndedEvent ended when ended.Reason is PlaybackEndReason.Stopped or PlaybackEndReason.Replaced:
                    await FinishCurrentAsync(QueueItemStatus.Skipped, PlaybackOutcome.Skipped, null, cancellationToken).ConfigureAwait(false);
                    await StartNextCoreAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case PlaybackFailedEvent failed:
                    await RecoverCoreAsync(failed.Failure, cancellationToken).ConfigureAwait(false);
                    break;
            }
            return Result<PlaybackOrchestrationState>.Success(Current);
        }
        finally { gate.Release(); }
    }

    public async Task<Result<PlaybackOrchestrationState>> StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            running = false;
            var stopped = await player.StopAsync(cancellationToken).ConfigureAwait(false);
            if (stopped.IsFailure) return Failure(stopped.Error.Code, stopped.Error.Message);
            return Result<PlaybackOrchestrationState>.Success(Current);
        }
        finally { gate.Release(); }
    }

    private async Task StartNextCoreAsync(CancellationToken cancellationToken)
    {
        if (!running || halted) return;
        var next = await store.GetNextAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (next is null)
        {
            active = null;
            return;
        }
        var playbackId = Guid.NewGuid();
        var history = await store.StartHistoryAsync(next, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        active = new ActivePlayback(next, playbackId, history.Id, QueueItemStatus.Preparing);
        await store.SetQueueStatusAsync(next.QueueItemId, QueueItemStatus.Preparing, null, cancellationToken).ConfigureAwait(false);
        if (next.PreflightFailure is not null)
        {
            await RecoverCoreAsync(next.PreflightFailure, cancellationToken).ConfigureAwait(false);
            return;
        }
        var loaded = await player.LoadAsync(new PlayerLoadRequest(playbackId, next.MediaPath), cancellationToken).ConfigureAwait(false);
        if (loaded.IsSuccess)
        {
            active.Status = QueueItemStatus.Playing;
            await store.SetQueueStatusAsync(next.QueueItemId, QueueItemStatus.Playing, null, cancellationToken).ConfigureAwait(false);
            return;
        }
        var failure = loaded.ValueOrFailure();
        await RecoverCoreAsync(failure, cancellationToken).ConfigureAwait(false);
    }

    private async Task RecoverCoreAsync(PlayerFailure failure, CancellationToken cancellationToken)
    {
        if (active is null) return;
        lastErrorCode = failure.Code;
        var result = await recovery.DecideAndRecordAsync(
            active.Item.MediaFileId == Guid.Empty ? null : active.Item.MediaFileId,
            PlaybackFailureStage.Playback,
            failure,
            active.RetryCount,
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            halted = true;
            return;
        }
        var decision = result.Value;
        if (decision.Action == PlaybackRecoveryAction.RetryCurrent)
        {
            active.RetryCount++;
            if (decision.Delay > TimeSpan.Zero) await Task.Delay(decision.Delay, clock, cancellationToken).ConfigureAwait(false);
            if (decision.RestartPlayer)
            {
                await player.StopAsync(cancellationToken).ConfigureAwait(false);
                active.PlaybackId = Guid.NewGuid();
                var start = await player.StartAsync(cancellationToken).ConfigureAwait(false);
                if (start.IsFailure)
                {
                    halted = true;
                    return;
                }
            }
            var load = await player.LoadAsync(new PlayerLoadRequest(active.PlaybackId, active.Item.MediaPath), cancellationToken).ConfigureAwait(false);
            if (load.IsSuccess)
            {
                active.Status = QueueItemStatus.Playing;
                await store.SetQueueStatusAsync(active.Item.QueueItemId, QueueItemStatus.Playing, null, cancellationToken).ConfigureAwait(false);
                return;
            }
            await RecoverCoreAsync(load.ValueOrFailure(), cancellationToken).ConfigureAwait(false);
            return;
        }
        await FinishCurrentAsync(QueueItemStatus.Failed, PlaybackOutcome.Failed, failure.Code, cancellationToken).ConfigureAwait(false);
        if (decision.Action == PlaybackRecoveryAction.HaltPlayback)
        {
            halted = true;
            return;
        }
        await StartNextCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FinishCurrentAsync(
        QueueItemStatus status,
        PlaybackOutcome outcome,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        if (active is null) return;
        var finished = active;
        active = null;
        var now = clock.GetUtcNow();
        await store.SetQueueStatusAsync(finished.Item.QueueItemId, status, now, cancellationToken).ConfigureAwait(false);
        await store.CompleteHistoryAsync(finished.HistoryId, outcome, now, errorCode, cancellationToken).ConfigureAwait(false);
    }

    private Result<PlaybackOrchestrationState> Failure(string code, string message)
    {
        running = false;
        lastErrorCode = code;
        return Result<PlaybackOrchestrationState>.Failure(new Error(code, message));
    }

    private sealed class ActivePlayback(
        PlayableQueueItem item,
        Guid playbackId,
        Guid historyId,
        QueueItemStatus status)
    {
        public PlayableQueueItem Item { get; } = item;
        public Guid PlaybackId { get; set; } = playbackId;
        public Guid HistoryId { get; } = historyId;
        public QueueItemStatus Status { get; set; } = status;
        public int RetryCount { get; set; }
    }
}

internal static class PlayerResultExtensions
{
    public static PlayerFailure ValueOrFailure(this Result<PlayerSnapshot> result)
    {
        if (result.IsSuccess && result.Value.Failure is not null) return result.Value.Failure;
        var error = result.Error;
        return new PlayerFailure(error.Code, PlayerFailureKind.Unknown, false, error.Message);
    }
}
