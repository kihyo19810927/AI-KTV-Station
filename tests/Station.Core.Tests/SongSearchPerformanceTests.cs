using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Station.Application.Search;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;
using Xunit.Abstractions;

namespace Station.Core.Tests;

public sealed class SongSearchPerformanceTests
{
    private readonly ITestOutputHelper output;

    public SongSearchPerformanceTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    [Trait("Category", "Performance")]
    public async Task One_hundred_thousand_documents_have_sub_200ms_p95_queries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-search-performance-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var database = new StationDbContext(options);
            await database.Database.MigrateAsync();
            await database.Database.ExecuteSqlRawAsync("""
                WITH RECURSIVE sequence(value) AS (
                    SELECT 1 UNION ALL SELECT value + 1 FROM sequence WHERE value < 100000
                )
                INSERT INTO SongSearchDocuments(SongId, Title, NormalizedTitle, Artists, Language, Category, Quality, Year, Availability, Terms)
                SELECT printf('00000000-0000-0000-0000-%012d', value),
                       printf('测试歌曲 %06d', value), printf('测试歌曲 %06d', value),
                       printf('歌手 %03d', value % 500),
                       CASE value % 3 WHEN 0 THEN '国语' WHEN 1 THEN '粤语' ELSE '英语' END,
                       CASE value % 2 WHEN 0 THEN '流行' ELSE '经典' END,
                       CASE value % 2 WHEN 0 THEN '4K' ELSE '1080P' END,
                       1980 + (value % 47), 'Available',
                       printf('测试歌曲 %06d ceshiqumu%06d csqm%06d 歌手%03d geshou%03d', value, value, value, value % 500, value % 500)
                FROM sequence
                """);
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
            output.WriteLine($"SEARCH_PERF count=100000 samples={samples.Count} p95_ms={p95:F2} max_ms={samples[^1]:F2}");
            Assert.True(p95 < 200, $"Search P95 was {p95:F2} ms; samples={string.Join(',', samples.Select(x => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
