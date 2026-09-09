using Station.Application.Common;
using Station.Application.Playback;
using Station.Domain.Models;

namespace Station.Core.Tests;

public sealed class QueuePlaybackOrchestratorTests
{
    [Fact]
    public async Task Completed_event_finishes_history_and_automatically_loads_next_song()
    {
        var player = new FakePlayer();
        var store = new MemoryPlaybackStore(Item(1), Item(2));
        var service = Create(player, store, maximumRetries: 0);

        var started = await service.StartAsync(store.RoomId);
        var firstPlayback = started.Value.PlaybackId!.Value;
        Assert.Equal(QueueItemStatus.Playing, store.Statuses[store.Items[0].QueueItemId]);

        await service.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, firstPlayback, PlaybackEndReason.Completed));

        Assert.Equal(PlaybackOutcome.Completed, store.Completed.Single().Outcome);
        Assert.Equal(store.Items[1].QueueItemId, service.Current.QueueItemId);
        Assert.Equal(2, player.Loads.Count);

        await service.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, service.Current.PlaybackId!.Value, PlaybackEndReason.Completed));
        Assert.Null(service.Current.QueueItemId);
        Assert.True(service.Current.IsRunning);
        Assert.Equal(2, store.Completed.Count);
    }

    [Fact]
    public async Task Retryable_process_failure_restarts_player_and_reloads_same_item()
    {
        var player = new FakePlayer();
        var store = new MemoryPlaybackStore(Item(1));
        var errors = new MemoryFailureStore();
        var service = Create(player, store, 1, errors);
        await service.StartAsync(store.RoomId);
        var playbackId = service.Current.PlaybackId!.Value;

        var failure = new PlayerFailure("player.command_timeout", PlayerFailureKind.CommandTimeout, true, "Interrupted");
        await service.HandleAsync(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, failure));

        Assert.Equal(store.Items[0].QueueItemId, service.Current.QueueItemId);
        Assert.Equal(1, service.Current.RetryCount);
        Assert.NotEqual(playbackId, service.Current.PlaybackId);
        Assert.Equal(2, player.Loads.Count);
        Assert.Equal(2, player.StartCount);
        Assert.Equal(1, player.StopCount);
        Assert.Single(errors.Errors);

        await service.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, service.Current.PlaybackId!.Value, PlaybackEndReason.Completed));
        Assert.Null(service.Current.QueueItemId);
        Assert.Equal(PlaybackOutcome.Completed, store.Completed.Single().Outcome);
    }

    [Fact]
    public async Task Exhausted_failure_marks_item_failed_and_advances_queue()
    {
        var player = new FakePlayer();
        var store = new MemoryPlaybackStore(Item(1), Item(2));
        var service = Create(player, store, maximumRetries: 0);
        await service.StartAsync(store.RoomId);
        var failedItem = service.Current.QueueItemId!.Value;

        var failure = new PlayerFailure("player.media_unavailable", PlayerFailureKind.MediaUnavailable, true, "Offline");
        await service.HandleAsync(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, service.Current.PlaybackId, failure));

        Assert.Equal(QueueItemStatus.Failed, store.Statuses[failedItem]);
        Assert.Equal(PlaybackOutcome.Failed, store.Completed.Single().Outcome);
        Assert.Equal(failure.Code, store.Completed.Single().ErrorCode);
        Assert.Equal(store.Items[1].QueueItemId, service.Current.QueueItemId);
    }

    [Fact]
    public async Task Systemic_failure_halts_without_starting_next_item()
    {
        var player = new FakePlayer();
        var store = new MemoryPlaybackStore(Item(1), Item(2));
        var service = Create(player, store, maximumRetries: 2);
        await service.StartAsync(store.RoomId);

        var failure = new PlayerFailure("player.executable_missing", PlayerFailureKind.ExecutableMissing, false, "Missing");
        await service.HandleAsync(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, service.Current.PlaybackId, failure));

        Assert.True(service.Current.IsHalted);
        Assert.Null(service.Current.QueueItemId);
        Assert.Single(player.Loads);
        Assert.Equal(QueueItemStatus.Failed, store.Statuses[store.Items[0].QueueItemId]);
    }

    [Fact]
    public async Task Stale_event_for_replaced_playback_is_ignored()
    {
        var player = new FakePlayer();
        var store = new MemoryPlaybackStore(Item(1), Item(2));
        var service = Create(player, store, maximumRetries: 0);
        await service.StartAsync(store.RoomId);
        var firstPlayback = service.Current.PlaybackId!.Value;
        await service.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, firstPlayback, PlaybackEndReason.Completed));
        var secondItem = service.Current.QueueItemId;

        await service.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, firstPlayback, PlaybackEndReason.Completed));

        Assert.Equal(secondItem, service.Current.QueueItemId);
        Assert.Single(store.Completed);
    }

    private static QueuePlaybackOrchestrator Create(
        FakePlayer player,
        MemoryPlaybackStore store,
        int maximumRetries,
        MemoryFailureStore? errors = null) =>
        new(player, store, new PlaybackRecoveryService(errors ?? new MemoryFailureStore(),
            new PlaybackRecoveryPolicy(maximumRetries, TimeSpan.Zero, TimeSpan.Zero)), TimeProvider.System);

    private static PlayableQueueItem Item(int index) => new(
        Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), $"X:\\fixture-{index}.mkv");

    private sealed class FakePlayer : IPlayerAdapter
    {
        private PlayerSnapshot snapshot = Snapshot(PlayerLifecycleState.Stopped);
        public List<PlayerLoadRequest> Loads { get; } = [];
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            snapshot = Snapshot(PlayerLifecycleState.Idle);
            return Success();
        }
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            snapshot = Snapshot(PlayerLifecycleState.Stopped);
            return Success();
        }
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default)
        {
            Loads.Add(request);
            snapshot = Snapshot(PlayerLifecycleState.Playing, request.PlaybackId);
            return Success();
        }
        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default) => Success();
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private Task<Result<PlayerSnapshot>> Success() => Task.FromResult(Result<PlayerSnapshot>.Success(snapshot));
        private static PlayerSnapshot Snapshot(PlayerLifecycleState state, Guid? id = null) =>
            new(state, id, TimeSpan.Zero, null, 70, null, null, []);
    }

    private sealed class MemoryPlaybackStore(params PlayableQueueItem[] source) : IPlaybackQueueStore
    {
        private int next;
        public Guid RoomId { get; } = Guid.NewGuid();
        public PlayableQueueItem[] Items { get; } = source.Select(x => x with { RoomId = Guid.Empty }).ToArray();
        public Dictionary<Guid, QueueItemStatus> Statuses { get; } = [];
        public List<(Guid HistoryId, PlaybackOutcome Outcome, string? ErrorCode)> Completed { get; } = [];

        public Task<PlayableQueueItem?> GetNextAsync(Guid roomId, CancellationToken cancellationToken = default)
        {
            if (next >= Items.Length) return Task.FromResult<PlayableQueueItem?>(null);
            var item = Items[next++] with { RoomId = roomId };
            Items[next - 1] = item;
            return Task.FromResult<PlayableQueueItem?>(item);
        }
        public Task SetQueueStatusAsync(Guid queueItemId, QueueItemStatus status, DateTimeOffset? completedAt, CancellationToken cancellationToken = default)
        { Statuses[queueItemId] = status; return Task.CompletedTask; }
        public Task<PlayHistory> StartHistoryAsync(PlayableQueueItem item, DateTimeOffset startedAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlayHistory { RoomSessionId = item.RoomId, SongId = item.SongId, QueueItemId = item.QueueItemId, StartedAt = startedAt });
        public Task CompleteHistoryAsync(Guid historyId, PlaybackOutcome outcome, DateTimeOffset endedAt, string? errorCode, CancellationToken cancellationToken = default)
        { Completed.Add((historyId, outcome, errorCode)); return Task.CompletedTask; }
    }

    private sealed class MemoryFailureStore : IPlaybackFailureStore
    {
        public List<PlaybackError> Errors { get; } = [];
        public Task RecordAsync(PlaybackError error, AvailabilityStatus? mediaAvailability, CancellationToken cancellationToken = default)
        { Errors.Add(error); return Task.CompletedTask; }
    }
}
