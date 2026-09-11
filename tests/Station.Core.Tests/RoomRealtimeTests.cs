using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Station.Application.Rooms;
using Station.Server.Api;
using Station.Server.Realtime;

namespace Station.Core.Tests;

public sealed class RoomRealtimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task SignalR_sends_live_event_and_reconnect_replays_missing_versions()
    {
        await using var factory = new StationApiTests.ApiFactory();
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/rooms", new { hostNickname = "主控" }))
            .Content.ReadFromJsonAsync<RoomCreatedResponse>(JsonOptions);
        Assert.NotNull(created);
        var guest = await (await client.PostAsJsonAsync("/api/rooms/join", new { joinCode = created.Room.JoinCode, nickname = "小满" }))
            .Content.ReadFromJsonAsync<IssuedRoomToken>(JsonOptions);
        Assert.NotNull(guest);

        await using var firstConnection = CreateConnection(factory);
        var received = new TaskCompletionSource<RoomRealtimeEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        firstConnection.On<RoomRealtimeEvent>("roomEvent", item => received.TrySetResult(item));
        await firstConnection.StartAsync();
        var initial = await firstConnection.InvokeAsync<RoomRealtimeSync>("Subscribe", guest.Token, null);
        Assert.NotNull(initial.Snapshot);
        Assert.Empty(initial.Events);

        RoomRealtimeEvent published;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            published = await factory.Services.GetRequiredService<IRoomRealtimePublisher>()
                .PublishAsync(created.Room.Id, "queue.added", new { ItemId = Guid.NewGuid() }, timeout.Token);
            Assert.Equal(published.Version, (await received.Task.WaitAsync(timeout.Token)).Version);
        }
        await firstConnection.StopAsync();

        var missed = await factory.Services.GetRequiredService<IRoomRealtimePublisher>()
            .PublishAsync(created.Room.Id, "queue.removed", new { ItemId = Guid.NewGuid() });
        await using var secondConnection = CreateConnection(factory);
        await secondConnection.StartAsync();
        var recovered = await secondConnection.InvokeAsync<RoomRealtimeSync>("Subscribe", guest.Token, published.Version);

        Assert.Null(recovered.Snapshot);
        Assert.Equal(missed.Version, Assert.Single(recovered.Events).Version);
        Assert.Equal("queue.removed", recovered.Events[0].Type);
    }

    [Fact]
    public void Journal_uses_web_casing_and_string_enums_for_browser_events()
    {
        var journal = new RoomRealtimeJournal();
        var eventItem = journal.Append(Guid.NewGuid(), "queue.status", new { ItemId = Guid.NewGuid(), Status = Station.Domain.Models.QueueItemStatus.Playing });

        Assert.Equal("queue.status", eventItem.Type);
        Assert.Equal("Playing", eventItem.Data.GetProperty("status").GetString());
        Assert.True(eventItem.Data.TryGetProperty("itemId", out _));
        Assert.False(eventItem.Data.TryGetProperty("ItemId", out _));
    }

    [Fact]
    public void Journal_returns_snapshot_requirement_when_client_falls_behind_retention()
    {
        var journal = new RoomRealtimeJournal();
        var roomId = Guid.NewGuid();
        for (var index = 0; index < 300; index++) journal.Append(roomId, "queue.changed", new { Index = index });

        Assert.False(journal.TryReadAfter(roomId, 1, out var unavailable));
        Assert.Empty(unavailable);
        Assert.True(journal.TryReadAfter(roomId, 299, out var latest));
        Assert.Equal(300, Assert.Single(latest).Version);
    }

    [Fact]
    public async Task Invalid_token_cannot_subscribe_or_join_room_group()
    {
        await using var factory = new StationApiTests.ApiFactory();
        await using var connection = CreateConnection(factory);
        await connection.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync<RoomRealtimeSync>("Subscribe", "invalid-token", null));

        Assert.Contains("auth.token_invalid", error.Message);
    }

    private static HubConnection CreateConnection(StationApiTests.ApiFactory factory) =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/room", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
}
