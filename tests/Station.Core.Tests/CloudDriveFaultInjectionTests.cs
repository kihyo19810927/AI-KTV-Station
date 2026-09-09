using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;
using Station.Infrastructure.Scanning;

namespace Station.Core.Tests;

public sealed class CloudDriveFaultInjectionTests
{
    [Fact]
    public async Task Http_403_marks_media_offline_then_readonly_rescan_restores_the_same_index_record()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-cloud-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var mediaPath = Path.Combine(root, "恢复歌曲.mkv");
        await File.WriteAllTextAsync(mediaPath, "fixture");
        try
        {
            await using var database = await CreateDatabaseAsync();
            var source = new MediaSource { Name = "Cloud fixture", RootPath = root, Availability = AvailabilityStatus.Available };
            var song = new Song { Title = "恢复歌曲", Availability = AvailabilityStatus.Available };
            var info = new FileInfo(mediaPath);
            var media = new MediaFile
            {
                Song = song,
                MediaSource = source,
                RelativePath = info.Name,
                SizeBytes = info.Length,
                LastWriteTime = info.LastWriteTimeUtc,
                Availability = AvailabilityStatus.Available,
                TrackMapping = new TrackMapping { BackingTrackId = 1, VocalTrackId = 2, IsManualOverride = true },
            };
            database.MediaFiles.Add(media);
            await database.SaveChangesAsync();
            var originalMediaId = media.Id;

            var failure = new PlayerFailure("player.media_http_403", PlayerFailureKind.MediaUnavailable, true, "Media is temporarily unavailable.");
            await new PlaybackRecoveryService(new EfPlaybackFailureStore(database), new PlaybackRecoveryPolicy())
                .DecideAndRecordAsync(media.Id, PlaybackFailureStage.Load, failure, 0);
            Assert.Equal(AvailabilityStatus.Offline, media.Availability);

            var scan = await new MediaScanService(new EfMediaScanRepository(database), new FileSystemMediaFileEnumerator()).ScanAsync(source.Id);

            Assert.True(scan.IsSuccess);
            database.ChangeTracker.Clear();
            var recovered = await database.MediaFiles.Include(x => x.TrackMapping).SingleAsync();
            Assert.Equal(originalMediaId, recovered.Id);
            Assert.Equal(AvailabilityStatus.Available, recovered.Availability);
            Assert.Null(recovered.LastErrorCode);
            Assert.True(recovered.TrackMapping!.IsManualOverride);
            Assert.Equal(1, await database.Songs.CountAsync());
            Assert.Equal(1, await database.PlaybackErrors.CountAsync());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Disconnected_mount_scan_records_error_and_recovery_scan_does_not_remove_existing_song()
    {
        await using var database = await CreateDatabaseAsync();
        var source = new MediaSource { Name = "Cloud fixture", RootPath = "unavailable", Availability = AvailabilityStatus.Available };
        var media = new MediaFile
        {
            Song = new Song { Title = "保留歌曲", Availability = AvailabilityStatus.Available },
            MediaSource = source,
            RelativePath = "保留歌曲.mkv",
            SizeBytes = 10,
            LastWriteTime = DateTimeOffset.UnixEpoch,
            Availability = AvailabilityStatus.Available,
        };
        database.MediaFiles.Add(media);
        await database.SaveChangesAsync();
        var enumerator = new RecoveringEnumerator();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), enumerator);

        var disconnected = await scanner.ScanAsync(source.Id);
        Assert.Equal(1, disconnected.Value.ErrorCount);
        Assert.Equal(AvailabilityStatus.Unknown, source.Availability);
        Assert.Equal(AvailabilityStatus.Available, media.Availability);

        enumerator.IsAvailable = true;
        var recovered = await scanner.ScanAsync(source.Id);
        Assert.Equal(0, recovered.Value.ErrorCount);
        Assert.Equal(AvailabilityStatus.Available, source.Availability);
        Assert.Equal(1, await database.Songs.CountAsync());
        Assert.Equal(1, await database.MediaFiles.CountAsync());
    }

    private static async Task<StationDbContext> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, true).Options);
        await database.Database.EnsureCreatedAsync();
        return database;
    }

    private sealed class RecoveringEnumerator : IMediaFileEnumerator
    {
        public bool IsAvailable { get; set; }
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (!IsAvailable)
            {
                yield return MediaEnumerationEntry.Error("", "media_source.path_unavailable");
                yield break;
            }
            yield return MediaEnumerationEntry.File("保留歌曲.mkv", 10, DateTimeOffset.UnixEpoch);
        }
    }
}
