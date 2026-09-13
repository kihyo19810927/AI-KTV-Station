using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;
using Xunit.Abstractions;

namespace Station.Core.Tests;

public sealed class ArtistBrowseBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "ManualBenchmark")]
    public async Task Measures_read_only_snapshot_without_exposing_catalog_contents()
    {
        var path = Environment.GetEnvironmentVariable("KTV_STATION_BENCHMARK_DB");
        if (!File.Exists(path)) return; // Explicit local benchmark input is intentionally never committed.
        var options = new DbContextOptionsBuilder<StationDbContext>()
            .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly }.ToString()).Options;
        await using var db = new StationDbContext(options);
        var songCount = await db.Songs.AsNoTracking().CountAsync();
        var clock = Stopwatch.StartNew();
        // Previous implementation materialized every song/artist relationship before grouping.
        var links = await db.SongArtists.AsNoTracking().Select(x => new { x.Artist.Name, x.SongId }).ToListAsync();
        var plays = await db.PlayHistory.AsNoTracking().GroupBy(x => x.SongId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var favorites = await db.Favorites.AsNoTracking().GroupBy(x => x.SongId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var oldCount = links.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => new { Name = x.Key, Score = x.Sum(v => plays.GetValueOrDefault(v.SongId) * 5 + favorites.GetValueOrDefault(v.SongId) * 8 + 1) }).OrderByDescending(x => x.Score).Count();
        var oldMs = clock.Elapsed.TotalMilliseconds;
        var service = new EfArtistBrowseService(db);
        clock.Restart();
        var first = await service.ListAsync(null, 5000);
        var coldMs = clock.Elapsed.TotalMilliseconds;
        var samples = new List<double>();
        for (var i = 0; i < 100; i++)
        {
            clock.Restart();
            await service.ListAsync(i % 2 == 0 ? null : "日本歌手", 3000);
            samples.Add(clock.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        Assert.Empty(db.ChangeTracker.Entries());
        output.WriteLine($"Songs={songCount}; Artists={oldCount}; Returned={first.Count}; LegacyMaterializationMs={oldMs:F2}; SqlColdMs={coldMs:F2}; CachedP50Ms={samples[49]:F3}; CachedP95Ms={samples[94]:F3}");
    }
}
