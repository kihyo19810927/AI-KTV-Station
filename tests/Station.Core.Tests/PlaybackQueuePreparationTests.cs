using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Media;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class PlaybackQueuePreparationTests
{
    [Fact]
    public async Task Probes_only_the_queue_head_and_reuses_its_fingerprint()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"ai-ktv-queue-probe-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;
            await using var database = new StationDbContext(options);
            await database.Database.MigrateAsync();

            var source = new MediaSource
            {
                Name = "fixture",
                RootPath = Path.Combine(Path.GetTempPath(), "ai-ktv-fixture"),
                Availability = AvailabilityStatus.Available,
            };
            var firstMedia = CreateMedia(source, "first.mkv", 10, DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
            var secondMedia = CreateMedia(source, "second.mkv", 20, DateTimeOffset.Parse("2026-09-02T00:00:00Z"));
            var room = new RoomSession { JoinCode = "123456", CreatedAt = DateTimeOffset.UtcNow };
            var guest = new Guest
            {
                RoomSession = room,
                Nickname = "访客1",
                TokenHash = Guid.NewGuid().ToString("N"),
                JoinedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            };
            var first = new QueueItem
            {
                RoomSession = room,
                Song = firstMedia.Song,
                RequestedByGuest = guest,
                Position = 1,
                Status = QueueItemStatus.ProbeFailed,
                RequestedAt = DateTimeOffset.UtcNow,
            };
            var second = new QueueItem
            {
                RoomSession = room,
                Song = secondMedia.Song,
                RequestedByGuest = guest,
                Position = 2,
                RequestedAt = DateTimeOffset.UtcNow,
            };
            database.AddRange(source, guest, first, second);
            await database.SaveChangesAsync();

            var probe = new RecordingProbe();
            var store = new EfPlaybackQueueStore(database, probe);

            var selectedFirst = await store.GetNextAsync(room.Id);
            Assert.Equal(first.Id, selectedFirst?.QueueItemId);
            Assert.Equal([Path.Combine(source.RootPath, "first.mkv")], probe.Paths);
            Assert.NotEmpty(firstMedia.ProbeFingerprint!);
            Assert.Equal(12.5, firstMedia.DurationSeconds);
            Assert.Null(secondMedia.ProbeFingerprint);

            _ = await store.GetNextAsync(room.Id);
            Assert.Single(probe.Paths);

            await store.SetQueueStatusAsync(first.Id, QueueItemStatus.Skipped, DateTimeOffset.UtcNow);
            var selectedSecond = await store.GetNextAsync(room.Id);
            Assert.Equal(second.Id, selectedSecond?.QueueItemId);
            Assert.Equal(2, probe.Paths.Count);
            Assert.Equal(Path.Combine(source.RootPath, "second.mkv"), probe.Paths[1]);
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static MediaFile CreateMedia(MediaSource source, string relativePath, long size, DateTimeOffset lastWriteTime)
    {
        var song = new Song
        {
            Title = Path.GetFileNameWithoutExtension(relativePath),
            Availability = AvailabilityStatus.Available,
        };
        var media = new MediaFile
        {
            Song = song,
            MediaSource = source,
            RelativePath = relativePath,
            SizeBytes = size,
            LastWriteTime = lastWriteTime,
            Availability = AvailabilityStatus.Available,
        };
        song.MediaFiles.Add(media);
        source.Files.Add(media);
        return media;
    }

    private sealed class RecordingProbe : IMediaProbe
    {
        public List<string> Paths { get; } = [];

        public Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default)
        {
            Paths.Add(mediaPath);
            return Task.FromResult(Result<MediaProbeResult>.Success(new MediaProbeResult(12.5, [])));
        }
    }
}
