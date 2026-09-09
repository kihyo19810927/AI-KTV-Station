using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Rooms;

namespace Station.Core.Tests;

public sealed class RoomLifecycleTests
{
    [Fact]
    public async Task Creates_one_open_room_and_returns_it_as_current()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var service = new RoomLifecycleService(new EfRoomRepository(database), new SequenceCodeGenerator("ABC234"));
        var created = await service.CreateAsync(5);
        Assert.True(created.IsSuccess);
        Assert.Equal("ABC234", created.Value.JoinCode);
        Assert.Equal(5, created.Value.MaxQueuedSongsPerGuest);
        var current = await service.GetCurrentAsync();
        Assert.Equal(created.Value.Id, current.Value!.Id);
        Assert.Equal("room.already_open", (await service.CreateAsync()).Error.Code);
        Assert.Equal(1, await database.RoomSessions.CountAsync());
    }

    [Fact]
    public async Task Close_is_idempotent_and_clears_current_room()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var service = new RoomLifecycleService(new EfRoomRepository(database), new SequenceCodeGenerator("ABC234"));
        var created = await service.CreateAsync();
        var closed = await service.CloseAsync(created.Value.Id);
        var closedAgain = await service.CloseAsync(created.Value.Id);
        Assert.Equal(RoomStatus.Closed, closed.Value.Status);
        Assert.NotNull(closed.Value.ClosedAt);
        Assert.Equal(closed.Value.ClosedAt, closedAgain.Value.ClosedAt);
        Assert.Null((await service.GetCurrentAsync()).Value);
    }

    [Fact]
    public async Task Host_can_update_open_room_queue_limit()
    {
        await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
        var service = new RoomLifecycleService(new EfRoomRepository(database), new SequenceCodeGenerator("ABC234"));
        var room = (await service.CreateAsync()).Value;
        Assert.Equal(25, (await service.SetQueueLimitAsync(room.Id, 25)).Value.MaxQueuedSongsPerGuest);
        Assert.Equal(25, (await database.RoomSessions.SingleAsync()).MaxQueuedSongsPerGuest);
        Assert.Equal("room.invalid_queue_limit", (await service.SetQueueLimitAsync(room.Id, 0)).Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Invalid_queue_limit_does_not_create_room(int limit)
    {
        var repository = new MemoryRoomRepository();
        var result = await new RoomLifecycleService(repository, new SequenceCodeGenerator("ABC234")).CreateAsync(limit);
        Assert.Equal("room.invalid_queue_limit", result.Error.Code);
        Assert.Empty(repository.Rooms);
    }

    [Fact]
    public async Task Code_collision_is_retried_without_reusing_closed_room_code()
    {
        var repository = new MemoryRoomRepository();
        repository.Rooms.Add(new RoomSession { JoinCode = "ABC234", Status = RoomStatus.Closed });
        var service = new RoomLifecycleService(repository, new SequenceCodeGenerator("ABC234", "XYZ789"));
        var result = await service.CreateAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal("XYZ789", result.Value.JoinCode);
    }

    [Fact]
    public async Task Open_room_is_recovered_from_sqlite_after_service_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-room-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            Guid roomId;
            await using (var first = new StationDbContext(options))
            {
                await first.Database.EnsureCreatedAsync();
                var created = await new RoomLifecycleService(new EfRoomRepository(first), new SequenceCodeGenerator("ABC234")).CreateAsync();
                roomId = created.Value.Id;
            }
            await using (var restarted = new StationDbContext(options))
            {
                var service = new RoomLifecycleService(new EfRoomRepository(restarted), new SequenceCodeGenerator("XYZ789"));
                var recovered = await service.GetCurrentAsync();
                Assert.Equal(roomId, recovered.Value!.Id);
                Assert.Equal(RoomStatus.Open, recovered.Value.Status);
                Assert.True((await service.CloseAsync(roomId)).IsSuccess);
            }
            await using (var afterClose = new StationDbContext(options))
                Assert.Null((await new RoomLifecycleService(new EfRoomRepository(afterClose), new SequenceCodeGenerator("ZZZ999")).GetCurrentAsync()).Value);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static StationDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
    }

    private sealed class SequenceCodeGenerator(params string[] codes) : IRoomJoinCodeGenerator
    {
        private readonly Queue<string> codes = new(codes);
        public string Create() => codes.Dequeue();
    }

    private sealed class MemoryRoomRepository : IRoomRepository
    {
        public List<RoomSession> Rooms { get; } = [];
        public Task<RoomSession?> FindOpenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rooms.SingleOrDefault(x => x.Status == RoomStatus.Open));
        public Task<RoomSession?> FindAsync(Guid roomId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rooms.SingleOrDefault(x => x.Id == roomId));
        public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rooms.Any(x => x.JoinCode == joinCode));
        public Task AddAsync(RoomSession room, CancellationToken cancellationToken = default) { Rooms.Add(room); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
