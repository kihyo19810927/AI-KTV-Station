using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Library;
using Station.Domain.Models;
using Station.Infrastructure.Library;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class ProfileLibraryTests
{
    [Fact]
    public async Task Creates_default_profile_with_system_playlists_and_resolves_device()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("爸爸", null);

        var profiles = await fixture.Service.ListProfilesAsync();
        Assert.Equal("爸爸", Assert.Single(profiles).DisplayName);
        Assert.False(profiles[0].RequiresPin);
        var playlists = await fixture.Service.ListPlaylistsAsync(created.Profile.Id);
        Assert.Equal(["我的收藏", "我常唱的"], playlists.Select(x => x.Name));

        var remembered = await fixture.Service.ResolveAsync(created.DeviceToken);
        Assert.NotNull(remembered);
        Assert.Equal(created.Profile.Id, remembered!.Profile.Id);
    }

    [Fact]
    public async Task Adds_favorite_and_creates_shared_custom_playlist_without_touching_legacy_favorites()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("妈妈", null);
        var favorite = await fixture.Service.SetFavoriteAsync(created.Profile.Id, fixture.Song.Id, true);
        var custom = await fixture.Service.CreatePlaylistAsync(created.Profile.Id, "过年气氛组", true);

        Assert.True(favorite.IsSuccess);
        Assert.True(custom.IsSuccess);
        Assert.True(custom.Value.IsFamilyShared);
        Assert.Empty(fixture.Database.Favorites);
        Assert.Single(fixture.Database.ProfilePlaylistItems);
    }

    [Fact]
    public async Task Profile_device_can_set_and_change_a_pin()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync("妈妈", null);

        Assert.True((await fixture.Service.SetPinAsync(created.Profile.Id, created.DeviceToken, null, "1234")).IsSuccess);
        Assert.Null(await fixture.Service.ActivateAsync(created.Profile.Id, "0000"));
        var reactivated = await fixture.Service.ActivateAsync(created.Profile.Id, "1234");
        Assert.NotNull(reactivated);
        Assert.True((await fixture.Service.SetPinAsync(created.Profile.Id, reactivated!.DeviceToken, "1234", "5678")).IsSuccess);
        Assert.Null(await fixture.Service.ActivateAsync(created.Profile.Id, "1234"));
        Assert.NotNull(await fixture.Service.ActivateAsync(created.Profile.Id, "5678"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(StationDbContext database, Song song) { Database = database; Song = song; Service = new ProfileLibraryService(new EfProfileLibraryRepository(database, TimeProvider.System)); }
        public StationDbContext Database { get; }
        public Song Song { get; }
        public ProfileLibraryService Service { get; }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
            var db = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
            await db.Database.EnsureCreatedAsync();
            var song = new Song { Title = "夜曲", NormalizedTitle = "夜曲", Availability = AvailabilityStatus.Available };
            db.Songs.Add(song); await db.SaveChangesAsync();
            return new Fixture(db, song);
        }
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
}
