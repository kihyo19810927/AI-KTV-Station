using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Media;
using Station.Application.Queue;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class QueuePreflightStatusTests
{
    [Theory]
    [InlineData(true, QueueItemStatus.Waiting)]
    [InlineData(false, QueueItemStatus.ProbeFailed)]
    public async Task Probe_result_controls_when_a_song_becomes_waiting(bool succeeds, QueueItemStatus expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-preflight-{Guid.NewGuid():N}.db");
        try
        {
            await using var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
            await database.Database.MigrateAsync();
            var room = new RoomSession { JoinCode = "ABC234", CreatedAt = DateTimeOffset.UtcNow };
            var guest = new Guest { RoomSession = room, Nickname = "访客1", TokenHash = "hash", JoinedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            var source = new MediaSource { Name = "fixture", RootPath = Path.GetTempPath(), Availability = AvailabilityStatus.Available };
            var song = new Song { Title = "测试歌", Availability = AvailabilityStatus.Available };
            song.MediaFiles.Add(new MediaFile { Song = song, MediaSource = source, RelativePath = "test.mkv", SizeBytes = 1, LastWriteTime = DateTimeOffset.UnixEpoch, Availability = AvailabilityStatus.Available });
            var item = new QueueItem { RoomSession = room, Song = song, RequestedByGuest = guest, Position = 1024, Status = QueueItemStatus.Probing, RequestedAt = DateTimeOffset.UtcNow };
            database.AddRange(source, guest, item); await database.SaveChangesAsync();
            var notifier = new RecordingNotifier();

            Assert.True(await new EfQueuePreflightService(database, new Probe(succeeds), notifier).ProbeNextWaitingAsync(room.Id));

            Assert.Equal(expected, (await database.QueueItems.SingleAsync()).Status);
            Assert.Equal((item.Id, expected), Assert.Single(notifier.Changes));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private sealed class Probe(bool succeeds) : IMediaProbe
    {
        public Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default) => Task.FromResult(succeeds
            ? Result<MediaProbeResult>.Success(new(10, []))
            : Result<MediaProbeResult>.Failure(new("media_probe.failed", "failed")));
    }
    private sealed class RecordingNotifier : IQueueStatusNotifier
    {
        public List<(Guid, QueueItemStatus)> Changes { get; } = [];
        public Task NotifyAsync(Guid roomId, Guid itemId, QueueItemStatus status, CancellationToken cancellationToken = default) { Changes.Add((itemId, status)); return Task.CompletedTask; }
    }
}
