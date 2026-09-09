using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Queue;

namespace Station.Core.Tests;

public sealed class RoomQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Requests_are_appended_with_stable_public_order()
    {
        await using var fixture = await QueueFixture.CreateAsync();
        var first = await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[0].Id);
        var second = await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[1].Id);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var queue = (await fixture.Service.ListAsync(fixture.GuestIdentity)).Value;
        Assert.Equal([first.Value.Id, second.Value.Id], queue.Select(x => x.Id));
        Assert.Equal(["Song 1", "Song 2"], queue.Select(x => x.Title));
        Assert.All(queue, x => Assert.Equal("访客", x.RequestedByNickname));
        Assert.DoesNotContain(typeof(QueueEntry).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Guest_limit_counts_active_requests_but_host_is_not_limited()
    {
        await using var fixture = await QueueFixture.CreateAsync(limit: 2);
        Assert.True((await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[0].Id)).IsSuccess);
        Assert.True((await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[1].Id)).IsSuccess);
        Assert.Equal("queue.guest_limit_reached", (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[2].Id)).Error.Code);

        Assert.True((await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[2].Id)).IsSuccess);
        Assert.True((await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[3].Id)).IsSuccess);
        Assert.True((await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[4].Id)).IsSuccess);
    }

    [Fact]
    public async Task Guest_removes_only_own_waiting_item_and_host_can_remove_any()
    {
        await using var fixture = await QueueFixture.CreateAsync();
        var own = (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[0].Id)).Value;
        var hosted = (await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[1].Id)).Value;

        Assert.Equal("queue.forbidden", (await fixture.Service.RemoveAsync(fixture.GuestIdentity, hosted.Id)).Error.Code);
        Assert.True((await fixture.Service.RemoveAsync(fixture.GuestIdentity, own.Id)).IsSuccess);
        Assert.True((await fixture.Service.RemoveAsync(fixture.HostIdentity, hosted.Id)).IsSuccess);
        Assert.Empty((await fixture.Service.ListAsync(fixture.HostIdentity)).Value);
        Assert.All(await fixture.Database.QueueItems.ToListAsync(), x => Assert.Equal(QueueItemStatus.Skipped, x.Status));
    }

    [Fact]
    public async Task Only_host_can_move_waiting_item_to_top()
    {
        await using var fixture = await QueueFixture.CreateAsync();
        var first = (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[0].Id)).Value;
        var second = (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[1].Id)).Value;

        Assert.Equal("queue.forbidden", (await fixture.Service.MoveToTopAsync(fixture.GuestIdentity, second.Id)).Error.Code);
        var moved = await fixture.Service.MoveToTopAsync(fixture.HostIdentity, second.Id);

        Assert.True(moved.IsSuccess);
        Assert.Equal([second.Id, first.Id], (await fixture.Service.ListAsync(fixture.GuestIdentity)).Value.Select(x => x.Id));
    }

    [Fact]
    public async Task Host_can_reorder_waiting_item_before_another_or_to_end()
    {
        await using var fixture = await QueueFixture.CreateAsync();
        var first = (await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[0].Id)).Value;
        var second = (await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[1].Id)).Value;
        var third = (await fixture.Service.RequestAsync(fixture.HostIdentity, fixture.Songs[2].Id)).Value;

        var before = await fixture.Service.ReorderBeforeAsync(fixture.HostIdentity, third.Id, second.Id);
        Assert.Equal([first.Id, third.Id, second.Id], before.Value.Select(x => x.Id));
        var end = await fixture.Service.ReorderBeforeAsync(fixture.HostIdentity, first.Id, null);
        Assert.Equal([third.Id, second.Id, first.Id], end.Value.Select(x => x.Id));
        Assert.Equal(3, end.Value.Select(x => x.Position).Distinct().Count());
    }

    [Fact]
    public async Task Concurrent_requests_are_serialized_without_duplicate_positions()
    {
        await using var fixture = await QueueFixture.CreateAsync(limit: 30, songCount: 20);
        var requests = fixture.Songs.Select(song => fixture.Service.RequestAsync(fixture.GuestIdentity, song.Id));

        var results = await Task.WhenAll(requests);

        Assert.All(results, x => Assert.True(x.IsSuccess, x.Error.Code));
        var queue = (await fixture.Service.ListAsync(fixture.GuestIdentity)).Value;
        Assert.Equal(20, queue.Count);
        Assert.Equal(20, queue.Select(x => x.Position).Distinct().Count());
        Assert.Equal(queue.OrderBy(x => x.Position).Select(x => x.Id), queue.Select(x => x.Id));
    }

    [Fact]
    public async Task Unavailable_song_and_invalid_identity_are_rejected_without_mutation()
    {
        await using var fixture = await QueueFixture.CreateAsync();
        fixture.Songs[0].Availability = AvailabilityStatus.Offline;
        await fixture.Database.SaveChangesAsync();
        Assert.Equal("queue.song_unavailable", (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[0].Id)).Error.Code);

        fixture.Guest.RevokedAt = Now;
        await fixture.Database.SaveChangesAsync();
        Assert.Equal("queue.identity_invalid", (await fixture.Service.RequestAsync(fixture.GuestIdentity, fixture.Songs[1].Id)).Error.Code);
        Assert.Empty(fixture.Database.QueueItems);
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private QueueFixture(StationDbContext database, RoomSession room, Guest guest, Guest host, Song[] songs)
        {
            Database = database;
            Guest = guest;
            Songs = songs;
            GuestIdentity = Identity(room, guest);
            HostIdentity = Identity(room, host);
            Service = new RoomQueueService(new EfRoomQueueRepository(database), new InProcessRoomQueueLock(), TimeProvider.System);
        }

        public StationDbContext Database { get; }
        public Guest Guest { get; }
        public Song[] Songs { get; }
        public RoomIdentity GuestIdentity { get; }
        public RoomIdentity HostIdentity { get; }
        public RoomQueueService Service { get; }

        public static async Task<QueueFixture> CreateAsync(int limit = 10, int songCount = 5)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>()
                .UseSqlite(connection, contextOwnsConnection: true).Options);
            await database.Database.EnsureCreatedAsync();
            var room = new RoomSession { JoinCode = "ABC234", CreatedAt = Now, Status = RoomStatus.Open, OpenSlot = 1, MaxQueuedSongsPerGuest = limit };
            var guest = CreateGuest(room, "访客", false);
            var host = CreateGuest(room, "主持人", true);
            var songs = Enumerable.Range(1, songCount).Select(index => new Song
            {
                Title = $"Song {index}",
                NormalizedTitle = $"song {index}",
                Availability = AvailabilityStatus.Available,
            }).ToArray();
            database.AddRange(room, guest, host);
            database.Songs.AddRange(songs);
            await database.SaveChangesAsync();
            return new QueueFixture(database, room, guest, host, songs);
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();

        private static Guest CreateGuest(RoomSession room, string name, bool host) => new()
        {
            RoomSessionId = room.Id,
            Nickname = name,
            TokenHash = Guid.NewGuid().ToString("N").PadRight(64, '0'),
            JoinedAt = Now,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            IsHost = host,
        };

        private static RoomIdentity Identity(RoomSession room, Guest guest) => new(
            room.Id, guest.Id, guest.Nickname, guest.IsHost ? RoomRole.Host : RoomRole.Guest, guest.ExpiresAt);
    }
}
