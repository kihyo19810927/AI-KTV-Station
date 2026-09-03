using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Scanning;

namespace Station.Core.Tests;

public sealed class MediaScanServiceTests
{
    [Fact]
    public async Task Scan_is_read_only_incremental_and_retains_missing_index()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "中文子目录"));
        var first = Path.Combine(root, "第一首.mkv");
        var second = Path.Combine(root, "中文子目录", "SECOND.MKV");
        await File.WriteAllTextAsync(first, "first"); await File.WriteAllTextAsync(second, "second"); await File.WriteAllTextAsync(Path.Combine(root, "ignore.txt"), "ignored");
        var original = await File.ReadAllTextAsync(first);
        try
        {
            await using var database = CreateDatabase();
            await database.Database.EnsureCreatedAsync();
            var source = new MediaSource { Name = "Fixture", RootPath = root, Availability = AvailabilityStatus.Available };
            database.MediaSources.Add(source); await database.SaveChangesAsync();
            var scanner = new MediaScanService(new EfMediaScanRepository(database), new FileSystemMediaFileEnumerator());
            var initial = await scanner.ScanAsync(source.Id);
            Assert.Equal(2, initial.Value.DiscoveredFiles); Assert.Equal(2, initial.Value.UpdatedFiles);
            var unchanged = await scanner.ScanAsync(source.Id);
            Assert.Equal(0, unchanged.Value.UpdatedFiles);
            Assert.Equal(original, await File.ReadAllTextAsync(first));
            File.Delete(first);
            var missing = await scanner.ScanAsync(source.Id);
            Assert.Equal(2, await database.MediaFiles.CountAsync());
            Assert.Equal(AvailabilityStatus.Offline, (await database.MediaFiles.SingleAsync(x => x.RelativePath == "第一首.mkv")).Availability);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Enumeration_error_does_not_mark_existing_files_offline()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        database.MediaFiles.Add(new MediaFile { MediaSource = source, RelativePath = "kept.mkv", Song = new Song { Title = "Kept", NormalizedTitle = "kept" }, Availability = AvailabilityStatus.Available });
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new ErrorEnumerator());
        var result = await scanner.ScanAsync(source.Id);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Equal(AvailabilityStatus.Available, (await database.MediaFiles.SingleAsync()).Availability);
    }

    [Fact]
    public async Task Cancellation_persists_run_checkpoint()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new CancellingEnumerator(cancellation));
        var result = await scanner.ScanAsync(source.Id, cancellation.Token);
        Assert.Equal(ScanStatus.Cancelled, result.Value.Status);
        Assert.Equal("first.mkv", result.Value.CheckpointRelativePath);
        Assert.Equal(ScanStatus.Cancelled, (await database.ScanRuns.SingleAsync()).Status);
    }

    private static StationDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        return new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
    }

    private sealed class ErrorEnumerator : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield(); yield return MediaEnumerationEntry.Error("blocked", "scan.directory_unreadable");
        }
    }

    private sealed class CancellingEnumerator(CancellationTokenSource cancellation) : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return MediaEnumerationEntry.File("first.mkv", 1, DateTimeOffset.UnixEpoch);
            cancellation.Cancel();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
