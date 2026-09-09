using Microsoft.EntityFrameworkCore;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Station.Core.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Additive_lyrics_migration_preserves_catalog_favorites_history_and_manual_mapping()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-upgrade-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            Guid songId;
            await using (var before = new StationDbContext(options))
            {
                await before.GetService<IMigrator>().MigrateAsync("20260910064000_AddSearchAddedAt");
                songId = Guid.NewGuid(); var sourceId = Guid.NewGuid(); var mediaId = Guid.NewGuid(); var roomId = Guid.NewGuid(); var guestId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
                var title = "保留歌曲"; var available = "Available"; var completed = "Completed";
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Songs (Id, Title, NormalizedTitle, SimplifiedTitle, TraditionalTitle, TitlePinyin, TitleInitials, CompactTitle, Availability) VALUES ({songId}, {title}, {title}, {title}, {title}, {string.Empty}, {string.Empty}, {title}, {available})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO MediaSources (Id, Name, RootPath, IsEnabled, Availability) VALUES ({sourceId}, {"年度目录"}, {"fixture"}, {true}, {available})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO MediaFiles (Id, SongId, MediaSourceId, RelativePath, SizeBytes, LastWriteTime, Availability) VALUES ({mediaId}, {songId}, {sourceId}, {"保留.mkv"}, {1L}, {now}, {available})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO TrackMappings (MediaFileId, BackingTrackId, VocalTrackId, IsManualOverride) VALUES ({mediaId}, {1}, {2}, {true})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RoomSessions (Id, JoinCode, Status, CreatedAt, MaxQueuedSongsPerGuest, OpenSlot) VALUES ({roomId}, {"ABC234"}, {"Open"}, {now}, {10}, {1})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Guests (Id, RoomSessionId, Nickname, TokenHash, JoinedAt, ExpiresAt, IsHost) VALUES ({guestId}, {roomId}, {"验收"}, {new string('a', 64)}, {now}, {now.AddHours(1)}, {false})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Favorites (GuestId, SongId, CreatedAt) VALUES ({guestId}, {songId}, {now})");
                await before.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PlayHistory (Id, RoomSessionId, SongId, Outcome, StartedAt) VALUES ({Guid.NewGuid()}, {roomId}, {songId}, {completed}, {now})");
            }
            await using (var after = new StationDbContext(options))
            {
                await after.Database.MigrateAsync();
                Assert.True(await after.Songs.AnyAsync(x => x.Id == songId));
                Assert.Single(await after.Favorites.ToListAsync()); Assert.Single(await after.PlayHistory.ToListAsync());
                Assert.True((await after.TrackMappings.SingleAsync()).IsManualOverride);
                Assert.Null((await after.MediaFiles.SingleAsync()).LyricsRelativePath);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

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
                Assert.Null(stored.MediaFiles.Single().LyricsRelativePath);
            }
        }
        finally
        {
            if (File.Exists(database)) File.Delete(database);
        }
    }
}
