using Microsoft.EntityFrameworkCore;
using Station.Application.Queue;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Queue;

public sealed class EfRoomQueueRepository(StationDbContext database) : IRoomQueueRepository
{
    private static readonly QueueItemStatus[] ActiveStatuses =
        [QueueItemStatus.Probing, QueueItemStatus.ProbeFailed, QueueItemStatus.Waiting, QueueItemStatus.Preparing, QueueItemStatus.Playing, QueueItemStatus.Paused];

    public Task<RoomSession?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        database.RoomSessions.SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);

    public Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default) =>
        database.Guests.SingleOrDefaultAsync(x => x.Id == guestId, cancellationToken);

    public Task<Song?> FindSongAsync(Guid songId, CancellationToken cancellationToken = default) =>
        database.Songs.Include(x => x.Artists).ThenInclude(x => x.Artist)
            .SingleOrDefaultAsync(x => x.Id == songId, cancellationToken);

    public Task<QueueItem?> FindItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        database.QueueItems.Include(x => x.Song).ThenInclude(x => x.Artists).ThenInclude(x => x.Artist)
            .Include(x => x.RequestedByGuest)
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);

    public async Task<IReadOnlyList<QueueItem>> ListActiveAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        await database.QueueItems.Include(x => x.Song).ThenInclude(x => x.Artists).ThenInclude(x => x.Artist)
            .Include(x => x.RequestedByGuest)
            .Where(x => x.RoomSessionId == roomId && ActiveStatuses.Contains(x.Status))
            .OrderBy(x => x.Position)
            .ToListAsync(cancellationToken);

    public Task<long?> GetMinPositionAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        database.QueueItems.Where(x => x.RoomSessionId == roomId).MinAsync(x => (long?)x.Position, cancellationToken);

    public Task<long?> GetMaxPositionAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        database.QueueItems.Where(x => x.RoomSessionId == roomId).MaxAsync(x => (long?)x.Position, cancellationToken);

    public Task AddAsync(QueueItem item, CancellationToken cancellationToken = default) =>
        database.QueueItems.AddAsync(item, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);

    public async Task SaveReorderAsync(IReadOnlyList<QueueItem> orderedItems, CancellationToken cancellationToken = default)
    {
        var finalPositions = orderedItems.Select(x => x.Position).ToArray();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        for (var index = 0; index < orderedItems.Count; index++) orderedItems[index].Position = long.MinValue + index;
        await database.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < orderedItems.Count; index++) orderedItems[index].Position = finalPositions[index];
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
