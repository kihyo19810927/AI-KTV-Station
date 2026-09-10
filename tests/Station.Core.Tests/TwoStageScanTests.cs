using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Media;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Scanning;

namespace Station.Core.Tests;

public sealed class TwoStageScanTests
{
    [Fact]
    public async Task Basic_index_is_durable_then_only_missing_or_changed_fingerprints_are_probed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection).Options;
        var source = new MediaSource { Name = "sample", RootPath = "fixture" };
        var entries = new TenFiles();
        var probe = new CountingProbe();
        await using (var db = new StationDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
            db.MediaSources.Add(source); await db.SaveChangesAsync();
            var scan = new MediaScanService(new EfMediaScanRepository(db), entries, mediaProbe: probe,
                scanOptions: new ScanOptions { BasicIndexOnly = true });
            var run = (await scan.ScanAsync(source.Id)).Value;
            Assert.Equal(10, run.IndexedFiles);
            Assert.Equal(0, probe.Calls);
            Assert.Equal(10, await db.MediaFiles.CountAsync());
        }
        await using (var db = new StationDbContext(dbOptions))
        {
            var scan = new MediaScanService(new EfMediaScanRepository(db), entries, mediaProbe: probe,
                scanOptions: new ScanOptions { ProbeConcurrency = 2 });
            Assert.Equal(10, (await scan.ScanAsync(source.Id)).Value.ProbedFiles);
            Assert.Equal(2, probe.Maximum);
            Assert.Equal(10, (await scan.ScanAsync(source.Id)).Value.CachedFiles);
            Assert.Equal(10, probe.Calls);
            entries.FirstSize++;
            Assert.Equal(1, (await scan.ScanAsync(source.Id)).Value.ProbedFiles);
            Assert.Equal(11, probe.Calls);
        }
    }

    [Fact]
    public async Task Cancelled_probe_keeps_successful_fingerprints_and_resumes_remaining_files()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection).Options;
        var source = new MediaSource { Name = "sample", RootPath = "fixture" };
        using var cancellation = new CancellationTokenSource();
        await using (var db = new StationDbContext(dbOptions))
        {
            await db.Database.EnsureCreatedAsync(); db.MediaSources.Add(source); await db.SaveChangesAsync();
            var scan = new MediaScanService(new EfMediaScanRepository(db), new TenFiles(), mediaProbe: new CountingProbe(),
                scanOptions: new ScanOptions { ProbeConcurrency = 1 });
            var run = await scan.ScanAsync(source.Id, Guid.NewGuid(), new CallbackProgress(p => { if (p.ProbedFiles == 3) cancellation.Cancel(); }), cancellation.Token);
            Assert.Equal(ScanStatus.Cancelled, run.Value.Status);
            Assert.Equal(10, await db.MediaFiles.CountAsync());
            Assert.Equal(3, await db.MediaFiles.CountAsync(x => x.ProbeFingerprint != null));
        }
        await using (var db = new StationDbContext(dbOptions))
        {
            var probe = new CountingProbe();
            var scan = new MediaScanService(new EfMediaScanRepository(db), new TenFiles(), mediaProbe: probe);
            var run = (await scan.ScanAsync(source.Id)).Value;
            Assert.Equal(3, run.CachedFiles);
            Assert.Equal(7, probe.Calls);
        }
    }

    private sealed class CallbackProgress(Action<MediaScanProgress> action) : IProgress<MediaScanProgress>
    {
        public void Report(MediaScanProgress value) => action(value);
    }
    private sealed class TenFiles : IMediaFileEnumerator
    {
        public long FirstSize { get; set; } = 100;
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return MediaEnumerationEntry.File($"歌手-歌曲{i}-国语-流行.mkv", i == 0 ? FirstSize : 100, DateTimeOffset.UnixEpoch);
                await Task.Yield();
            }
        }
    }
    private sealed class CountingProbe : IMediaProbe
    {
        private int active;
        public int Calls { get; private set; }
        public int Maximum { get; private set; }
        public async Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default)
        {
            Calls++; active++; Maximum = Math.Max(Maximum, active);
            try { await Task.Delay(20, cancellationToken); return Result<MediaProbeResult>.Success(new(10, [])); }
            finally { active--; }
        }
    }
}
