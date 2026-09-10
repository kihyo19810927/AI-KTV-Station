using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Rooms;

namespace Station.Core.Tests;

public sealed class RoomAuthenticationTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Guest_token_is_random_scoped_hashed_and_validated()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var room = await AddOpenRoomAsync(database);
        var clock = new AdjustableTimeProvider(Start);
        var service = new RoomAuthenticationService(new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), clock);

        var issued = await service.JoinAsync(room.JoinCode.ToLowerInvariant(), "  小满  ");

        Assert.True(issued.IsSuccess);
        Assert.Equal(RoomRole.Guest, issued.Value.Role);
        Assert.Equal("小满", issued.Value.Nickname);
        Assert.Equal(Start.AddHours(12), issued.Value.ExpiresAt);
        var stored = await database.Guests.SingleAsync();
        Assert.NotEqual(issued.Value.Token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        var identity = await service.ValidateAsync(issued.Value.Token);
        Assert.True(identity.IsSuccess);
        Assert.Equal(room.Id, identity.Value.RoomId);
        Assert.Equal(issued.Value.GuestId, identity.Value.GuestId);
        Assert.Equal(RoomRole.Guest, identity.Value.Role);
    }

    [Fact]
    public async Task Host_token_has_separate_role_and_shorter_lifetime()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var room = await AddOpenRoomAsync(database);
        var service = new RoomAuthenticationService(
            new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), new AdjustableTimeProvider(Start));

        var host = await service.IssueHostAsync(room.Id, "主持人");
        var guest = await service.JoinAsync(room.JoinCode, "访客");

        Assert.True(host.IsSuccess);
        Assert.Equal(RoomRole.Host, host.Value.Role);
        Assert.Equal(Start.AddHours(8), host.Value.ExpiresAt);
        Assert.NotEqual(host.Value.Token, guest.Value.Token);
        Assert.Equal(RoomRole.Host, (await service.ValidateAsync(host.Value.Token)).Value.Role);
    }

    [Fact]
    public async Task Admin_guest_list_contains_public_state_but_never_token_hash()
    {
        await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
        var room = await AddOpenRoomAsync(database);
        var service = new RoomAuthenticationService(new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), new AdjustableTimeProvider(Start));
        await service.IssueHostAsync(room.Id, "主持人"); await service.JoinAsync(room.JoinCode, "访客");

        var guests = await service.ListGuestsAsync(room.Id);

        Assert.Equal(2, guests.Value.Count);
        Assert.Contains(guests.Value, x => x.Role == RoomRole.Host); Assert.Contains(guests.Value, x => x.Role == RoomRole.Guest);
        Assert.DoesNotContain(typeof(RoomGuestAdminDetails).GetProperties(), x => x.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Expired_revoked_invalid_and_closed_room_tokens_are_rejected()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var room = await AddOpenRoomAsync(database);
        var clock = new AdjustableTimeProvider(Start);
        var service = new RoomAuthenticationService(new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), clock);
        var first = (await service.JoinAsync(room.JoinCode, "甲")).Value;
        var second = (await service.JoinAsync(room.JoinCode, "乙")).Value;

        Assert.Equal("auth.token_invalid", (await service.ValidateAsync("not-a-token")).Error.Code);
        Assert.True((await service.RevokeAsync(first.GuestId)).IsSuccess);
        Assert.Equal("auth.token_revoked", (await service.ValidateAsync(first.Token)).Error.Code);
        Assert.True((await service.RevokeAsync(first.GuestId)).IsSuccess);
        clock.Advance(TimeSpan.FromHours(13));
        Assert.Equal("auth.token_expired", (await service.ValidateAsync(second.Token)).Error.Code);

        var third = (await service.JoinAsync(room.JoinCode, "丙")).Value;
        room.Status = RoomStatus.Closed;
        room.OpenSlot = null;
        room.ClosedAt = clock.GetUtcNow();
        await database.SaveChangesAsync();
        Assert.Equal("auth.room_closed", (await service.ValidateAsync(third.Token)).Error.Code);
    }

    [Theory]
    [InlineData("BAD", "访客", "auth.invalid_join_code")]
    public async Task Invalid_join_input_does_not_persist_identity(string code, string nickname, string expectedError)
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        await AddOpenRoomAsync(database);
        var service = new RoomAuthenticationService(
            new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), new AdjustableTimeProvider(Start));

        var result = await service.JoinAsync(code, nickname);

        Assert.Equal(expectedError, result.Error.Code);
        Assert.Empty(database.Guests);
    }

    [Fact]
    public void Authorization_policy_limits_guest_mutations()
    {
        Assert.True(RoomAuthorizationPolicy.Allows(RoomRole.Guest, RoomPermission.RequestSong));
        Assert.True(RoomAuthorizationPolicy.Allows(RoomRole.Guest, RoomPermission.RemoveOwnRequest));
        Assert.True(RoomAuthorizationPolicy.Allows(RoomRole.Guest, RoomPermission.ControlPlayback));
        Assert.False(RoomAuthorizationPolicy.Allows(RoomRole.Guest, RoomPermission.RemoveAnyRequest));
        Assert.All(Enum.GetValues<RoomPermission>(), permission =>
            Assert.True(RoomAuthorizationPolicy.Allows(RoomRole.Host, permission)));
    }

    [Fact]
    public async Task Blank_guest_names_are_assigned_in_room_sequence()
    {
        await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
        var room = await AddOpenRoomAsync(database);
        var service = new RoomAuthenticationService(new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), new AdjustableTimeProvider(Start));

        Assert.Equal("访客1", (await service.JoinAsync(room.JoinCode, string.Empty)).Value.Nickname);
        Assert.Equal("访客2", (await service.JoinAsync(room.JoinCode, " ")).Value.Nickname);
    }

    private static StationDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return new StationDbContext(new DbContextOptionsBuilder<StationDbContext>()
            .UseSqlite(connection, contextOwnsConnection: true).Options);
    }

    private static async Task<RoomSession> AddOpenRoomAsync(StationDbContext database)
    {
        var room = new RoomSession
        {
            JoinCode = "ABC234",
            CreatedAt = Start,
            Status = RoomStatus.Open,
            OpenSlot = 1,
        };
        database.RoomSessions.Add(room);
        await database.SaveChangesAsync();
        return room;
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan value) => current = current.Add(value);
    }
}
