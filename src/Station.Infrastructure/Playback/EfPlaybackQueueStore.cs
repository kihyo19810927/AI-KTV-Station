using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfPlaybackQueueStore(StationDbContext database) : IPlaybackQueueStore
{
    public async Task<PlayableQueueItem?> GetNextAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var item = await database.QueueItems
            .Include(x => x.Song).ThenInclude(x => x.MediaFiles).ThenInclude(x => x.MediaSource)
            .Where(x => x.RoomSessionId == roomId && x.Status == QueueItemStatus.Waiting)
            .OrderBy(x => x.Position)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        var candidates = item.Song.MediaFiles
            .Where(x => x.MediaSource.IsEnabled)
            .OrderBy(x => x.Id)
            .ToArray();
        var media = candidates
            .Where(x => x.Availability == AvailabilityStatus.Available && x.MediaSource.IsEnabled && x.MediaSource.Availability == AvailabilityStatus.Available)
            .FirstOrDefault();
        if (media is null)
        {
            var knownMedia = candidates.FirstOrDefault();
            if (knownMedia is not null)
                return new(item.Id, roomId, item.SongId, knownMedia.Id,
                    Path.Combine(knownMedia.MediaSource.RootPath, knownMedia.RelativePath),
                    new PlayerFailure("player.media_unavailable", PlayerFailureKind.MediaUnavailable, true, "Song media is currently unavailable."));
            return new(item.Id, roomId, item.SongId, Guid.Empty, string.Empty,
                new PlayerFailure("player.media_missing", PlayerFailureKind.MediaLoadFailed, false, "Song has no indexed media."));
        }
        return new(item.Id, roomId, item.SongId, media.Id, Path.Combine(media.MediaSource.RootPath, media.RelativePath));
    }

    public async Task SetQueueStatusAsync(Guid queueItemId, QueueItemStatus status, DateTimeOffset? completedAt, CancellationToken cancellationToken = default)
    {
        var item = await database.QueueItems.SingleAsync(x => x.Id == queueItemId, cancellationToken);
        item.Status = status;
        item.CompletedAt = completedAt;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlayHistory> StartHistoryAsync(PlayableQueueItem item, DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        var history = new PlayHistory
        {
            RoomSessionId = item.RoomId,
            SongId = item.SongId,
            QueueItemId = item.QueueItemId,
            StartedAt = startedAt,
        };
        await database.PlayHistory.AddAsync(history, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return history;
    }

    public async Task CompleteHistoryAsync(Guid historyId, PlaybackOutcome outcome, DateTimeOffset endedAt, string? errorCode, CancellationToken cancellationToken = default)
    {
        var history = await database.PlayHistory.SingleAsync(x => x.Id == historyId, cancellationToken);
        history.Outcome = outcome;
        history.EndedAt = endedAt;
        history.ErrorCode = errorCode;
        await database.SaveChangesAsync(cancellationToken);
    }
}
