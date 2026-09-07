using Microsoft.EntityFrameworkCore;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Migration_creates_schema_and_round_trips_catalog_graph()
    {
        var database = Path.Combine(Path.GetTempPath(), $"ai-ktv-station-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options;
            await using (var context = new StationDbContext(options))
            {
                await context.Database.MigrateAsync();
                var song = new Song
                {
                    Title = "夜空中最亮的星",
                    NormalizedTitle = "夜空中最亮的星",
                    SimplifiedTitle = "夜空中最亮的星",
                    TraditionalTitle = "夜空中最亮的星",
                    TitlePinyin = "yekongzhongzuiliangdexing",
                    TitleInitials = "ykzzldx",
                    CompactTitle = "夜空中最亮的星",
                    Availability = AvailabilityStatus.Offline,
                };
                var source = new MediaSource { Name = "Test", RootPath = "fixture", Availability = AvailabilityStatus.Offline };
                song.MediaFiles.Add(new MediaFile { MediaSource = source, RelativePath = "Unicode 测试/歌曲.mkv", Availability = AvailabilityStatus.Offline });
                context.Songs.Add(song);
                await context.SaveChangesAsync();
            }
            await using (var context = new StationDbContext(options))
            {
                var stored = await context.Songs.Include(x => x.MediaFiles).SingleAsync();
                Assert.Equal(AvailabilityStatus.Offline, stored.Availability);
                Assert.Equal("yekongzhongzuiliangdexing", stored.TitlePinyin);
                Assert.Equal("ykzzldx", stored.TitleInitials);
                Assert.Equal("Unicode 测试/歌曲.mkv", stored.MediaFiles.Single().RelativePath);
            }
        }
        finally
        {
            if (File.Exists(database)) File.Delete(database);
        }
    }
}
