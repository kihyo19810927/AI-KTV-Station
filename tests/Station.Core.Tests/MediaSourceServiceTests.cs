using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Station.Application.MediaSources;
using Station.Infrastructure.MediaSources;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class MediaSourceServiceTests
{
    [Fact]
    public async Task Adds_multiple_sources_and_public_projection_hides_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-source-{Guid.NewGuid():N}");
        var first = Path.Combine(root, "一号曲库");
        var second = Path.Combine(root, "二号曲库");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        try
        {
            await using var database = CreateDatabase();
            await database.Database.EnsureCreatedAsync();
            var service = new MediaSourceService(new EfMediaSourceRepository(database), new FileSystemMediaPathInspector());
            Assert.True((await service.AddAsync("主曲库", first)).IsSuccess);
            var added = await service.AddAsync("备用曲库", second);
            Assert.True(added.IsSuccess);
            Assert.True((await service.SetEnabledAsync(added.Value.Id, false)).Value.IsEnabled is false);
            var summaries = await service.ListPublicAsync();
            Assert.Equal(2, summaries.Count);
            Assert.DoesNotContain(summaries.SelectMany(x => x.GetType().GetProperties()), property => property.Name.Contains("Path", StringComparison.Ordinal));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Rejects_missing_and_duplicate_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var database = CreateDatabase();
            await database.Database.EnsureCreatedAsync();
            var service = new MediaSourceService(new EfMediaSourceRepository(database), new FileSystemMediaPathInspector());
            Assert.Equal("media_source.path_missing", (await service.AddAsync("Missing", Path.Combine(root, "missing"))).Error.Code);
            Assert.True((await service.AddAsync("First", root)).IsSuccess);
            Assert.Equal("media_source.duplicate_path", (await service.AddAsync("Duplicate", root)).Error.Code);
        }
        finally { Directory.Delete(root, true); }
    }

    private static StationDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
    }
}
