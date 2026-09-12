using System.Collections.Concurrent;
using Station.Application.Common;
using Station.Application.Rooms;
using Station.Domain.Models;

namespace Station.Application.Queue;

public sealed record QueueEntry(
    Guid Id,
    Guid SongId,
    string Title,
    Guid RequestedByGuestId,
    string RequestedByNickname,
    long Position,
    QueueItemStatus Status,
    DateTimeOffset RequestedAt,
    string? Artists = null);

public interface IRoomQueueService
{
    Task<Result<IReadOnlyList<QueueEntry>>> ListAsync(RoomIdentity identity, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<QueueEntry>> MoveToTopAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<QueueEntry>> InsertNextAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<QueueEntry>>> ReorderBeforeAsync(RoomIdentity identity, Guid itemId, Guid? beforeItemId, CancellationToken cancellationToken = default);
}

public interface IRoomQueueRepository
{
    Task<RoomSession?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default);
    Task<Song?> FindSongAsync(Guid songId, CancellationToken cancellationToken = default);
    Task<QueueItem?> FindItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QueueItem>> ListActiveAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<long?> GetMaxPositionAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task AddAsync(QueueItem item, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task SaveReorderAsync(IReadOnlyList<QueueItem> orderedItems, CancellationToken cancellationToken = default);
}

public interface IQueueStatusNotifier
{
    Task NotifyAsync(Guid roomId, Guid itemId, QueueItemStatus status, CancellationToken cancellationToken = default);
}

public interface IRoomQueueLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(Guid roomId, CancellationToken cancellationToken = default);
}

public sealed class InProcessRoomQueueLock : IRoomQueueLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var gate = locks.GetOrAdd(roomId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class RoomQueueService(
    IRoomQueueRepository repository,
    IRoomQueueLock queueLock,
    TimeProvider clock) : IRoomQueueService
{
    private const long PositionStep = 1024;

    public async Task<Result<QueueEntry>> RequestAsync(
        RoomIdentity identity,
        Guid songId,
        CancellationToken cancellationToken = default)
    {
        if (songId == Guid.Empty) return Failure<QueueEntry>("queue.invalid_song", "Song id is required.");
        await using var lease = await queueLock.AcquireAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<QueueEntry>.Failure(context.Error);
        var song = await repository.FindSongAsync(songId, cancellationToken).ConfigureAwait(false);
        if (song is null) return Failure<QueueEntry>("queue.song_not_found", "Song was not found.");
        if (song.Availability != AvailabilityStatus.Available)
            return Failure<QueueEntry>("queue.song_unavailable", "Song has no available media.");
        var active = await repository.ListActiveAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var ownCount = active.Count(x => x.RequestedByGuestId == identity.GuestId);
        if (!identity.Role.Equals(RoomRole.Host) && ownCount >= context.Value.Room.MaxQueuedSongsPerGuest)
            return Failure<QueueEntry>("queue.guest_limit_reached", "Guest queue limit was reached.");
        var maxPosition = await repository.GetMaxPositionAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var position = maxPosition is null ? PositionStep : checked(maxPosition.Value + PositionStep);
        var item = new QueueItem
        {
            RoomSessionId = identity.RoomId,
            SongId = song.Id,
            Song = song,
            RequestedByGuestId = identity.GuestId,
            RequestedByGuest = context.Value.Guest,
            Position = position,
            Status = QueueItemStatus.Probing,
            RequestedAt = clock.GetUtcNow(),
        };
        await repository.AddAsync(item, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<QueueEntry>.Success(Map(item));
    }

    public async Task<Result<bool>> RemoveAsync(
        RoomIdentity identity,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty) return Failure<bool>("queue.invalid_item", "Queue item id is required.");
        await using var lease = await queueLock.AcquireAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<bool>.Failure(context.Error);
        var item = await repository.FindItemAsync(itemId, cancellationToken).ConfigureAwait(false);
        if (item is null || item.RoomSessionId != identity.RoomId)
            return Failure<bool>("queue.item_not_found", "Queue item was not found.");
        if (!IsQueued(item.Status))
            return Failure<bool>("queue.item_not_mutable", "Only queued items can be removed.");
        if (identity.Role != RoomRole.Host && item.RequestedByGuestId != identity.GuestId)
            return Failure<bool>("queue.forbidden", "Guests can remove only their own requests.");
        item.Status = QueueItemStatus.Skipped;
        item.CompletedAt = clock.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<QueueEntry>> MoveToTopAsync(
        RoomIdentity identity,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (!RoomAuthorizationPolicy.Allows(identity.Role, RoomPermission.ReorderQueue))
            return Failure<QueueEntry>("queue.forbidden", "Host permission is required.");
        await using var lease = await queueLock.AcquireAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<QueueEntry>.Failure(context.Error);
        var active = await repository.ListActiveAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var item = active.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return Failure<QueueEntry>("queue.item_not_found", "Queue item was not found.");
        if (!IsQueued(item.Status))
            return Failure<QueueEntry>("queue.item_not_mutable", "Only queued items can be reordered.");
        var first = active.Where(x => IsQueued(x.Status)).OrderBy(x => x.Position).First();
        if (first.Id != item.Id)
        {
            item.Position = checked(first.Position - PositionStep);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Result<QueueEntry>.Success(Map(item));
    }

    public async Task<Result<IReadOnlyList<QueueEntry>>> ReorderBeforeAsync(
        RoomIdentity identity,
        Guid itemId,
        Guid? beforeItemId,
        CancellationToken cancellationToken = default)
    {
        if (!RoomAuthorizationPolicy.Allows(identity.Role, RoomPermission.ReorderQueue))
            return Failure<IReadOnlyList<QueueEntry>>("queue.forbidden", "Host permission is required.");
        if (itemId == Guid.Empty || beforeItemId == Guid.Empty || beforeItemId == itemId)
            return Failure<IReadOnlyList<QueueEntry>>("queue.invalid_reorder", "Queue reorder target is invalid.");
        await using var lease = await queueLock.AcquireAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<IReadOnlyList<QueueEntry>>.Failure(context.Error);
        var active = await repository.ListActiveAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var waiting = active.Where(x => IsQueued(x.Status)).OrderBy(x => x.Position).ToList();
        var moving = waiting.SingleOrDefault(x => x.Id == itemId);
        if (moving is null) return Failure<IReadOnlyList<QueueEntry>>("queue.item_not_mutable", "Only waiting items can be reordered.");
        waiting.Remove(moving);
        var targetIndex = beforeItemId is null ? waiting.Count : waiting.FindIndex(x => x.Id == beforeItemId);
        if (targetIndex < 0) return Failure<IReadOnlyList<QueueEntry>>("queue.target_not_found", "Queue reorder target was not found.");
        waiting.Insert(targetIndex, moving);
        for (var index = 0; index < waiting.Count; index++) waiting[index].Position = checked((index + 1L) * PositionStep);
        await repository.SaveReorderAsync(waiting, cancellationToken).ConfigureAwait(false);
        var nonWaiting = active.Where(x => !IsQueued(x.Status));
        return Result<IReadOnlyList<QueueEntry>>.Success(nonWaiting.Concat(waiting).OrderBy(x => x.Position).Select(Map).ToArray());
    }

    public async Task<Result<QueueEntry>> InsertNextAsync(
        RoomIdentity identity,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty) return Failure<QueueEntry>("queue.invalid_item", "Queue item id is required.");
        await using var lease = await queueLock.AcquireAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<QueueEntry>.Failure(context.Error);
        var active = await repository.ListActiveAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        var item = active.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return Failure<QueueEntry>("queue.item_not_found", "Queue item was not found.");
        if (identity.Role != RoomRole.Host && item.RequestedByGuestId != identity.GuestId)
            return Failure<QueueEntry>("queue.forbidden", "Guests can insert only their own requests.");
        if (!IsQueued(item.Status)) return Failure<QueueEntry>("queue.item_not_mutable", "Only queued items can be inserted.");
        var first = active.Where(x => IsQueued(x.Status)).OrderBy(x => x.Position).First();
        if (first.Id != item.Id)
        {
            item.Position = checked(first.Position - PositionStep);
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Result<QueueEntry>.Success(Map(item));
    }

    public async Task<Result<IReadOnlyList<QueueEntry>>> ListAsync(
        RoomIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var context = await ValidateContextAsync(identity, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure) return Result<IReadOnlyList<QueueEntry>>.Failure(context.Error);
        var items = await repository.ListActiveAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<QueueEntry>>.Success(items.Select(Map).ToArray());
    }

    private async Task<Result<(RoomSession Room, Guest Guest)>> ValidateContextAsync(
        RoomIdentity identity,
        CancellationToken cancellationToken)
    {
        var room = await repository.FindRoomAsync(identity.RoomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.Status != RoomStatus.Open)
            return Failure<(RoomSession, Guest)>("queue.room_closed", "The room is not open.");
        var guest = await repository.FindGuestAsync(identity.GuestId, cancellationToken).ConfigureAwait(false);
        if (guest is null || guest.RoomSessionId != room.Id || guest.RevokedAt is not null || guest.ExpiresAt <= clock.GetUtcNow())
            return Failure<(RoomSession, Guest)>("queue.identity_invalid", "Room identity is no longer valid.");
        return Result<(RoomSession, Guest)>.Success((room, guest));
    }

    private static QueueEntry Map(QueueItem item) => new(
        item.Id,
        item.SongId,
        item.Song.Title,
        item.RequestedByGuestId,
        item.RequestedByGuest.Nickname,
        item.Position,
        item.Status,
        item.RequestedAt,
        string.Join(" / ", item.Song.Artists.OrderBy(x => x.Order).Select(x => x.Artist.Name)));

    private static bool IsQueued(QueueItemStatus status) => status is QueueItemStatus.Probing or QueueItemStatus.ProbeFailed or QueueItemStatus.Waiting;

    private static Result<T> Failure<T>(string code, string message) => Result<T>.Failure(new Error(code, message));
}
