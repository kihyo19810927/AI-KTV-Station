using Microsoft.EntityFrameworkCore;
using Station.Application.Media;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfQueuePreflightService(StationDbContext database, IMediaProbe mediaProbe, IQueueStatusNotifier? notifier = null) : IQueuePreflightService
{
    public async Task<bool> ProbeNextWaitingAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var item = await database.QueueItems
            .Include(item => item.Song).ThenInclude(song => song.MediaFiles).ThenInclude(file => file.MediaSource)
            .Where(item => item.RoomSessionId == roomId && item.Status == QueueItemStatus.Probing)
            .OrderBy(item => item.Position)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return false;
        var media = item.Song.MediaFiles.Where(file => file.Availability == AvailabilityStatus.Available
                && file.MediaSource.IsEnabled && file.MediaSource.Availability == AvailabilityStatus.Available)
            .OrderBy(file => file.Id).FirstOrDefault();
        if (media is null)
        {
            item.Status = QueueItemStatus.ProbeFailed;
            await database.SaveChangesAsync(cancellationToken);
            if (notifier is not null) await notifier.NotifyAsync(roomId, item.Id, item.Status, cancellationToken);
            return true;
        }

        if (!string.IsNullOrEmpty(media.ProbeFingerprint))
        {
            item.Status = QueueItemStatus.Waiting;
            await database.SaveChangesAsync(cancellationToken);
            if (notifier is not null) await notifier.NotifyAsync(roomId, item.Id, item.Status, cancellationToken);
            return true;
        }
        var result = await mediaProbe.ProbeAsync(Path.Combine(media.MediaSource.RootPath, media.RelativePath), cancellationToken);
        if (result.IsSuccess)
        {
            media.DurationSeconds = result.Value.DurationSeconds;
            media.ProbeFingerprint = MediaScanService.Fingerprint(media.MediaSource, media);
            media.LastErrorCode = null;
            item.Status = QueueItemStatus.Waiting;
        }
        else
        {
            media.LastErrorCode = result.Error.Code;
            item.Status = QueueItemStatus.ProbeFailed;
        }
        await database.SaveChangesAsync(cancellationToken);
        if (notifier is not null) await notifier.NotifyAsync(roomId, item.Id, item.Status, cancellationToken);
        return true;
    }
}
