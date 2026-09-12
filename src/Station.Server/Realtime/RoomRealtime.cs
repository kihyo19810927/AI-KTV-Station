using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Domain.Models;

namespace Station.Server.Realtime;

public sealed record RoomRealtimeEvent(long Version, string Type, JsonElement Data, DateTimeOffset OccurredAt);
public sealed record RoomRealtimeSnapshot(
    long Version,
    Guid RoomId,
    IReadOnlyList<QueueEntry> Queue,
    PlaybackProgress? Playback);
public sealed record RoomRealtimeSync(
    long Version,
    RoomRealtimeSnapshot? Snapshot,
    IReadOnlyList<RoomRealtimeEvent> Events);

public sealed class RoomRealtimeJournal
{
    private const int EventLimit = 256;
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
    private readonly ConcurrentDictionary<Guid, RoomJournal> rooms = new();

    public RoomRealtimeEvent Append(Guid roomId, string type, object payload)
    {
        var journal = rooms.GetOrAdd(roomId, static _ => new RoomJournal());
        lock (journal.Gate)
        {
            var item = new RoomRealtimeEvent(++journal.Version, type, JsonSerializer.SerializeToElement(payload, EventJsonOptions), DateTimeOffset.UtcNow);
            journal.Events.Enqueue(item);
            while (journal.Events.Count > EventLimit) journal.Events.Dequeue();
            return item;
        }
    }

    public long CurrentVersion(Guid roomId)
    {
        if (!rooms.TryGetValue(roomId, out var journal)) return 0;
        lock (journal.Gate) return journal.Version;
    }

    public bool TryReadAfter(Guid roomId, long afterVersion, out IReadOnlyList<RoomRealtimeEvent> events)
    {
        if (!rooms.TryGetValue(roomId, out var journal))
        {
            events = [];
            return afterVersion == 0;
        }
        lock (journal.Gate)
        {
            if (afterVersion == journal.Version)
            {
                events = [];
                return true;
            }
            var available = journal.Events.Where(x => x.Version > afterVersion).ToArray();
            var contiguous = available.Length > 0 && available[0].Version == afterVersion + 1;
            events = contiguous ? available : [];
            return contiguous;
        }
    }

    private sealed class RoomJournal
    {
        public object Gate { get; } = new();
        public Queue<RoomRealtimeEvent> Events { get; } = new();
        public long Version { get; set; }
    }
}

public interface IRoomRealtimePublisher
{
    Task<RoomRealtimeEvent> PublishAsync(Guid roomId, string type, object payload, CancellationToken cancellationToken = default);
}

public sealed class SignalRQueueStatusNotifier(IRoomRealtimePublisher publisher) : IQueueStatusNotifier
{
    public async Task NotifyAsync(Guid roomId, Guid itemId, QueueItemStatus status, CancellationToken cancellationToken = default) =>
        _ = await publisher.PublishAsync(roomId, "queue.status", new { ItemId = itemId, Status = status }, cancellationToken);
}

public sealed class SignalRRoomRealtimePublisher(
    RoomRealtimeJournal journal,
    IHubContext<RoomHub> hub) : IRoomRealtimePublisher
{
    public async Task<RoomRealtimeEvent> PublishAsync(Guid roomId, string type, object payload, CancellationToken cancellationToken = default)
    {
        var item = journal.Append(roomId, type, payload);
        await hub.Clients.Group(RoomHub.Group(roomId)).SendAsync("roomEvent", item, cancellationToken);
        return item;
    }
}

public sealed class RoomHub(
    RoomAuthenticationService authentication,
    RoomQueueService queue,
    PlaybackControlService playback,
    RoomRealtimeJournal journal) : Hub
{
    public async Task<RoomRealtimeSync> Subscribe(string token, long? afterVersion = null)
    {
        var identity = await authentication.ValidateAsync(token, Context.ConnectionAborted);
        if (identity.IsFailure) throw new HubException(identity.Error.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(identity.Value.RoomId), Context.ConnectionAborted);
        if (afterVersion is > 0 && journal.TryReadAfter(identity.Value.RoomId, afterVersion.Value, out var events))
        {
            var latestVersion = journal.CurrentVersion(identity.Value.RoomId);
            return new(latestVersion, null, events);
        }
        var queueResult = await queue.ListAsync(identity.Value, Context.ConnectionAborted);
        if (queueResult.IsFailure) throw new HubException(queueResult.Error.Code);
        var playbackResult = await playback.GetProgressAsync(Context.ConnectionAborted);
        var currentVersion = journal.CurrentVersion(identity.Value.RoomId);
        var snapshot = new RoomRealtimeSnapshot(
            currentVersion,
            identity.Value.RoomId,
            queueResult.Value,
            playbackResult.IsSuccess ? playbackResult.Value : null);
        return new(currentVersion, snapshot, []);
    }

    internal static string Group(Guid roomId) => $"room:{roomId:N}";
}
