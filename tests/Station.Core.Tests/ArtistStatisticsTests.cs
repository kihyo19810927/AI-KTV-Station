using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class ArtistStatisticsTests
{
    [Fact]
    public async Task Sql_aggregation_is_untracked_and_successful_write_invalidates_shared_cache()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection).Options;
        var cache = new ArtistBrowseCache();
        await using var database = new StationDbContext(options, cache);
        await database.Database.EnsureCreatedAsync();
        var artist = new Artist { Name = "TestArtist" };
        var song = new Song { Title = "TestSong" };
        database.SongArtists.Add(new SongArtist { Artist = artist, Song = song });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var service = new EfArtistBrowseService(database, cache: cache);
        Assert.Equal(1, Assert.Single(await service.ListAsync(null)).SongCount);
        Assert.Empty(database.ChangeTracker.Entries());
        database.SongArtists.Add(new SongArtist { ArtistId = artist.Id, Song = new Song { Title = "Second" } });
        await database.SaveChangesAsync();
        Assert.Equal(2, Assert.Single(await service.ListAsync(null)).SongCount);
    }
}
