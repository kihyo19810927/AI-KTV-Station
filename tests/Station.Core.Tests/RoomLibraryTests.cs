using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Library;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Library;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class RoomLibraryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Favorite_add_and_remove_are_idempotent_and_guest_scoped()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Service.SetFavoriteAsync(fixture.Identity, fixture.Songs[0].Id, true)).IsSuccess);
        Assert.True((await fixture.Service.SetFavoriteAsync(fixture.Identity, fixture.Songs[0].Id, true)).IsSuccess);

        var favorites = (await fixture.Service.ListFavoritesAsync(fixture.Identity)).Value;
        Assert.Equal("夜曲", Assert.Single(favorites).Title);
        Assert.Equal("周杰伦", favorites[0].Artists);
        Assert.DoesNotContain(typeof(FavoriteSong).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));

        Assert.True((await fixture.Service.SetFavoriteAsync(fixture.Identity, fixture.Songs[0].Id, false)).IsSuccess);
        Assert.True((await fixture.Service.SetFavoriteAsync(fixture.Identity, fixture.Songs[0].Id, false)).IsSuccess);
        Assert.Empty((await fixture.Service.ListFavoritesAsync(fixture.Identity)).Value);
    }

    [Fact]
    public async Task History_is_room_scoped_newest_first_and_paged()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.PlayHistory.AddRange(
            History(fixture.Room.Id, fixture.Songs[0].Id, Now.AddMinutes(-3)),
            History(fixture.Room.Id, fixture.Songs[1].Id, Now.AddMinutes(-1)),
            History(Guid.NewGuid(), fixture.Songs[0].Id, Now));
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Service.ListHistoryAsync(fixture.Identity, 1, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Total);
        Assert.Equal("后来", Assert.Single(result.Value.Items).Title);
        Assert.Equal("library.invalid_paging", (await fixture.Service.ListHistoryAsync(fixture.Identity, 0, 20)).Error.Code);
    }

    [Fact]
    public async Task Popular_counts_completed_plays_only_with_stable_ranking()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Database.PlayHistory.AddRange(
            History(fixture.Room.Id, fixture.Songs[0].Id, Now.AddMinutes(-4)),
            History(fixture.Room.Id, fixture.Songs[0].Id, Now.AddMinutes(-3)),
            History(fixture.Room.Id, fixture.Songs[1].Id, Now.AddMinutes(-2)),
            History(fixture.Room.Id, fixture.Songs[1].Id, Now.AddMinutes(-1), PlaybackOutcome.Failed));
        await fixture.Database.SaveChangesAsync();

        var popular = (await fixture.Service.ListPopularAsync(fixture.Identity)).Value;

        Assert.Equal(fixture.Songs[0].Id, popular[0].SongId);
        Assert.Equal(2, popular[0].PlayCount);
        Assert.Equal(1, popular[1].PlayCount);
        Assert.DoesNotContain(typeof(PopularSong).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Revoked_identity_and_unknown_song_are_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal("library.song_not_found", (await fixture.Service.SetFavoriteAsync(fixture.Identity, Guid.NewGuid(), true)).Error.Code);
        fixture.Guest.RevokedAt = Now;
        await fixture.Database.SaveChangesAsync();
        Assert.Equal("library.identity_invalid", (await fixture.Service.ListFavoritesAsync(fixture.Identity)).Error.Code);
    }

    private static PlayHistory History(Guid roomId, Guid songId, DateTimeOffset started, PlaybackOutcome outcome = PlaybackOutcome.Completed) => new()
    {
        RoomSessionId = roomId,
        SongId = songId,
        StartedAt = started,
        EndedAt = started.AddMinutes(4),
        Outcome = outcome,
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(StationDbContext database, RoomSession room, Guest guest, Song[] songs)
        {
            Database = database; Room = room; Guest = guest; Songs = songs;
            Identity = new(room.Id, guest.Id, guest.Nickname, RoomRole.Guest, guest.ExpiresAt);
            Service = new(new EfRoomLibraryRepository(database), TimeProvider.System);
        }
        public StationDbContext Database { get; }
        public RoomSession Room { get; }
        public Guest Guest { get; }
        public Song[] Songs { get; }
        public RoomIdentity Identity { get; }
        public RoomLibraryService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
            await database.Database.EnsureCreatedAsync();
            var room = new RoomSession { JoinCode = "ABC234", CreatedAt = Now, Status = RoomStatus.Open, OpenSlot = 1 };
            var guest = new Guest { RoomSessionId = room.Id, Nickname = "小满", TokenHash = new string('A', 64), JoinedAt = Now, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            var artist = new Artist { Name = "周杰伦", NormalizedName = "周杰伦" };
            var songs = new[] { Song("夜曲"), Song("后来") };
            songs[0].Artists.Add(new SongArtist { Song = songs[0], SongId = songs[0].Id, Artist = artist, ArtistId = artist.Id, Order = 0 });
            database.AddRange(room, guest, artist);
            database.Songs.AddRange(songs);
            await database.SaveChangesAsync();
            return new(database, room, guest, songs);
        }
        public ValueTask DisposeAsync() => Database.DisposeAsync();
        private static Song Song(string title) => new() { Title = title, NormalizedTitle = title, Availability = AvailabilityStatus.Available };
    }
}
