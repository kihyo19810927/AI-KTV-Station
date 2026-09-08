using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class PlaybackRecoveryTests
{
    [Fact]
    public void Retryable_media_failure_uses_bounded_backoff_then_skips()
    {
        var policy = new PlaybackRecoveryPolicy(2, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150));
        var failure = Failure(PlayerFailureKind.MediaUnavailable, retryable: true);
        var first = policy.Decide(failure, 0);
        var second = policy.Decide(failure, 1);
        var exhausted = policy.Decide(failure, 2);
        Assert.Equal(PlaybackRecoveryAction.RetryCurrent, first.Action);
        Assert.Equal(TimeSpan.FromMilliseconds(100), first.Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(150), second.Delay);
        Assert.Equal(PlaybackRecoveryAction.SkipCurrent, exhausted.Action);
    }

    [Theory]
    [InlineData(PlayerFailureKind.ProcessExited)]
    [InlineData(PlayerFailureKind.ConnectionTimeout)]
    [InlineData(PlayerFailureKind.CommandTimeout)]
    [InlineData(PlayerFailureKind.ProtocolError)]
    public void Transport_failure_requires_player_restart(PlayerFailureKind kind)
    {
        var decision = new PlaybackRecoveryPolicy().Decide(Failure(kind, retryable: true), 0);
        Assert.Equal(PlaybackRecoveryAction.RetryCurrent, decision.Action);
        Assert.True(decision.RestartPlayer);
    }

    [Fact]
    public void Unsupported_media_skips_but_missing_executable_halts()
    {
        var policy = new PlaybackRecoveryPolicy();
        Assert.Equal(PlaybackRecoveryAction.SkipCurrent, policy.Decide(Failure(PlayerFailureKind.Unsupported, false), 0).Action);
        Assert.Equal(PlaybackRecoveryAction.HaltPlayback, policy.Decide(Failure(PlayerFailureKind.ExecutableMissing, false), 0).Action);
    }

    [Fact]
    public async Task Service_records_sanitized_failure_and_availability_impact()
    {
        var store = new RecordingStore();
        var result = await new PlaybackRecoveryService(store, new PlaybackRecoveryPolicy())
            .DecideAndRecordAsync(Guid.NewGuid(), PlaybackFailureStage.Load,
                new PlayerFailure("player.media_http_403", PlayerFailureKind.MediaUnavailable, true, "Media is unavailable."), 0);
        Assert.True(result.IsSuccess);
        Assert.Equal(AvailabilityStatus.Offline, store.Availability);
        Assert.Equal("player.media_http_403", store.Error!.ErrorCode);
        Assert.Equal("MediaUnavailable:RetryCurrent", store.Error.DiagnosticSummary);
        Assert.DoesNotContain("\\", store.Error.DiagnosticSummary);
    }

    [Fact]
    public async Task Negative_retry_count_is_rejected_without_recording()
    {
        var store = new RecordingStore();
        var result = await new PlaybackRecoveryService(store, new PlaybackRecoveryPolicy())
            .DecideAndRecordAsync(null, PlaybackFailureStage.Playback, Failure(PlayerFailureKind.Unknown, true), -1);
        Assert.Equal("playback_recovery.invalid_retry_count", result.Error.Code);
        Assert.Null(store.Error);
    }

    [Fact]
    public async Task Ef_store_marks_media_offline_but_retains_catalog_and_error_history()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "fixture", RootPath = "fixture" };
        var song = new Song { Title = "保留歌曲", Availability = AvailabilityStatus.Available };
        var media = new MediaFile { Song = song, MediaSource = source, RelativePath = "保留.mkv", Availability = AvailabilityStatus.Available };
        database.MediaFiles.Add(media);
        await database.SaveChangesAsync();

        var failure = new PlayerFailure("player.media_unavailable", PlayerFailureKind.MediaUnavailable, true, "Media is unavailable.");
        var result = await new PlaybackRecoveryService(new EfPlaybackFailureStore(database), new PlaybackRecoveryPolicy())
            .DecideAndRecordAsync(media.Id, PlaybackFailureStage.Load, failure, 0);

        Assert.True(result.IsSuccess);
        database.ChangeTracker.Clear();
        Assert.Equal(1, await database.Songs.CountAsync());
        Assert.Equal(1, await database.MediaFiles.CountAsync());
        Assert.Equal(AvailabilityStatus.Offline, (await database.MediaFiles.SingleAsync()).Availability);
        Assert.Equal("player.media_unavailable", (await database.PlaybackErrors.SingleAsync()).ErrorCode);
    }

    [Fact]
    public async Task Ef_store_keeps_song_available_when_an_alternative_media_file_is_online()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "fixture", RootPath = "fixture" };
        var song = new Song { Title = "双版本", Availability = AvailabilityStatus.Available };
        var failed = new MediaFile { Song = song, MediaSource = source, RelativePath = "offline.mkv", Availability = AvailabilityStatus.Available };
        song.MediaFiles.Add(new MediaFile { Song = song, MediaSource = source, RelativePath = "online.mkv", Availability = AvailabilityStatus.Available });
        database.MediaFiles.Add(failed);
        await database.SaveChangesAsync();

        await new EfPlaybackFailureStore(database).RecordAsync(
            new PlaybackError { MediaFileId = failed.Id, ErrorCode = "player.media_unavailable", Stage = "Load", IsRetryable = true },
            AvailabilityStatus.Offline);

        database.ChangeTracker.Clear();
        Assert.Equal(AvailabilityStatus.Available, (await database.Songs.SingleAsync()).Availability);
        Assert.Equal(1, await database.MediaFiles.CountAsync(x => x.Availability == AvailabilityStatus.Available));
        Assert.Equal(1, await database.MediaFiles.CountAsync(x => x.Availability == AvailabilityStatus.Offline));
    }

    private static PlayerFailure Failure(PlayerFailureKind kind, bool retryable) =>
        new($"player.{kind.ToString().ToLowerInvariant()}", kind, retryable, "Playback failed.");

    private sealed class RecordingStore : IPlaybackFailureStore
    {
        public PlaybackError? Error { get; private set; }
        public AvailabilityStatus? Availability { get; private set; }
        public Task RecordAsync(PlaybackError error, AvailabilityStatus? mediaAvailability, CancellationToken cancellationToken = default)
        {
            Error = error;
            Availability = mediaAvailability;
            return Task.CompletedTask;
        }
    }
}
