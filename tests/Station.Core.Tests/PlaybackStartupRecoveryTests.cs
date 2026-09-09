using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class PlaybackStartupRecoveryTests
{
    [Theory]
    [InlineData(QueueItemStatus.Preparing)]
    [InlineData(QueueItemStatus.Playing)]
    [InlineData(QueueItemStatus.Paused)]
    public async Task Process_restart_requeues_interrupted_item_and_closes_abandoned_history(QueueItemStatus interruptedStatus)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-restart-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            Guid queueItemId;
            await using (var before = new StationDbContext(options))
            {
                await before.Database.MigrateAsync();
                var room = new RoomSession { JoinCode = "RST123", CreatedAt = DateTimeOffset.UtcNow, OpenSlot = 1 };
                var guest = new Guest { RoomSession = room, Nickname = "guest", TokenHash = new string('a', 64), JoinedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
                var song = new Song { Title = "断点歌曲", Availability = AvailabilityStatus.Available };
                var item = new QueueItem { RoomSession = room, Song = song, RequestedByGuest = guest, Position = 1, Status = interruptedStatus, RequestedAt = DateTimeOffset.UtcNow };
                before.QueueItems.Add(item);
                before.PlayHistory.Add(new PlayHistory { RoomSessionId = room.Id, SongId = song.Id, QueueItemId = item.Id, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
                await before.SaveChangesAsync();
                queueItemId = item.Id;
            }

            await using (var restarted = new StationDbContext(options))
            {
                var result = await new PlaybackStartupRecoveryService(new EfPlaybackStartupRecoveryStore(restarted), TimeProvider.System).RecoverAsync();
                Assert.True(result.IsSuccess);
                Assert.Equal(1, result.Value.RequeuedItems);
                Assert.Equal(1, result.Value.ClosedHistories);
            }

            await using (var verification = new StationDbContext(options))
            {
                var item = await verification.QueueItems.SingleAsync(x => x.Id == queueItemId);
                var history = await verification.PlayHistory.SingleAsync();
                Assert.Equal(QueueItemStatus.Waiting, item.Status);
                Assert.Null(item.CompletedAt);
                Assert.Equal(PlaybackOutcome.Failed, history.Outcome);
                Assert.NotNull(history.EndedAt);
                Assert.Equal("playback.interrupted_by_restart", history.ErrorCode);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Startup_recovery_is_idempotent_and_does_not_change_completed_work()
    {
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new StationDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var service = new PlaybackStartupRecoveryService(new EfPlaybackStartupRecoveryStore(database), TimeProvider.System);

        var first = await service.RecoverAsync();
        var second = await service.RecoverAsync();

        Assert.Equal(new PlaybackStartupRecoveryResult(0, 0), first.Value);
        Assert.Equal(new PlaybackStartupRecoveryResult(0, 0), second.Value);
    }
}
