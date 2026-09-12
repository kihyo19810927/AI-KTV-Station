using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;
using Xunit.Abstractions;

namespace Station.Core.Tests;

public sealed class SongSearchPerformanceTests
{
    private readonly ITestOutputHelper output;

    public SongSearchPerformanceTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(100000, 200)]
    [InlineData(300000, 300)]
    [Trait("Category", "Performance")]
    public async Task Large_catalog_has_bounded_p95_queries(int documentCount, double maximumP95Milliseconds)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-search-performance-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var database = new StationDbContext(options);
            await database.Database.MigrateAsync();
            await database.Database.ExecuteSqlRawAsync("""
                WITH RECURSIVE sequence(value) AS (
                    SELECT 1 UNION ALL SELECT value + 1 FROM sequence WHERE value < {0}
                )
                INSERT INTO SongSearchDocuments(SongId, Title, NormalizedTitle, Artists, Language, Category, Quality, Year, Availability, Terms, TitleTerms, ArtistTerms)
                SELECT printf('00000000-0000-0000-0000-%012d', value),
                       printf('测试歌曲 %06d', value), printf('测试歌曲 %06d', value),
                       printf('歌手 %03d', value % 500),
                       CASE value % 3 WHEN 0 THEN '国语' WHEN 1 THEN '粤语' ELSE '英语' END,
                       CASE value % 2 WHEN 0 THEN '流行' ELSE '经典' END,
                       CASE value % 2 WHEN 0 THEN '4K' ELSE '1080P' END,
                       1980 + (value % 47), 'Available',
                       printf('测试歌曲 %06d ceshiqumu%06d csqm%06d 歌手%03d geshou%03d', value, value, value, value % 500, value % 500),
                       printf('测试歌曲 %06d ceshiqumu%06d csqm%06d', value, value, value),
                       printf('歌手%03d geshou%03d', value % 500, value % 500)
                FROM sequence
                """, documentCount);
            var index = new SqliteSongSearchIndex(database, new ToolGoodSearchTextNormalizer());
            _ = await index.SearchAsync(new SongSearchQuery("ceshiqumu050000", PageSize: 20));
            var samples = new List<double>();
            for (var indexValue = 0; indexValue < 30; indexValue++)
            {
                var stopwatch = Stopwatch.StartNew();
                var text = indexValue % 2 == 0
                    ? $"ceshiqumu{(indexValue * 3001 + 1):D6}"
                    : $"geshou{indexValue % 500:D3}";
                var result = await index.SearchAsync(new SongSearchQuery(text, PageSize: 20));
                stopwatch.Stop();
                Assert.True(result.IsSuccess, result.Error.Code);
                Assert.NotEmpty(result.Value.Items);
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            samples.Sort();
            var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
            output.WriteLine($"SEARCH_PERF count={documentCount} samples={samples.Count} p95_ms={p95:F2} max_ms={samples[^1]:F2}");
            Assert.True(p95 < maximumP95Milliseconds, $"Search P95 was {p95:F2} ms; samples={string.Join(',', samples.Select(x => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(100000, 30)]
    [InlineData(300000, 90)]
    [Trait("Category", "Performance")]
    public async Task Incremental_scanner_processes_large_generated_enumerations(int fileCount, double maximumSeconds)
    {
        var repository = new CountingScanRepository();
        var scanner = new MediaScanService(repository, new GeneratedMediaEnumerator(fileCount));
        var stopwatch = Stopwatch.StartNew();

        var result = await scanner.ScanAsync(repository.Source.Id);

        stopwatch.Stop();
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(fileCount, result.Value.DiscoveredFiles);
        Assert.Equal(fileCount, result.Value.UpdatedFiles);
        Assert.Equal(fileCount, repository.AddedFiles);
        output.WriteLine($"SCAN_PERF count={fileCount} elapsed_s={stopwatch.Elapsed.TotalSeconds:F2} files_per_s={fileCount / stopwatch.Elapsed.TotalSeconds:F0}");
        Assert.True(stopwatch.Elapsed.TotalSeconds < maximumSeconds, $"Generated scan took {stopwatch.Elapsed.TotalSeconds:F2}s for {fileCount} files.");
    }

    private sealed class GeneratedMediaEnumerator(int count) : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return MediaEnumerationEntry.File($"年度/歌手-{index:D6}-国语-流行.mkv", 1024 + index, DateTimeOffset.UnixEpoch);
            }
        }
    }

    private sealed class CountingScanRepository : IMediaScanRepository
    {
        public MediaSource Source { get; } = new() { Name = "Generated", RootPath = "fixture", Availability = AvailabilityStatus.Available };
        public int AddedFiles { get; private set; }
        public Task<MediaSource?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<MediaSource?>(sourceId == Source.Id ? Source : null);
        public Task<IReadOnlyList<MediaFile>> ListFilesAsync(Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaFile>>([]);
        public Task AddFileAsync(MediaFile file, CancellationToken cancellationToken = default) { AddedFiles++; return Task.CompletedTask; }
        public Task AddTrackAsync(MediaTrack track, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
