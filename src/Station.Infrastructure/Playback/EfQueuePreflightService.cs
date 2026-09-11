using Microsoft.EntityFrameworkCore;
using Station.Application.Media;
using Station.Application.Playback;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfQueuePreflightService(StationDbContext database, IMediaProbe mediaProbe) : IQueuePreflightService
{
    public async Task<bool> ProbeNextWaitingAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var media = await database.QueueItems
            .Where(item => item.RoomSessionId == roomId && item.Status == QueueItemStatus.Waiting)
            .OrderBy(item => item.Position)
            .SelectMany(item => item.Song.MediaFiles
                .Where(file => file.Availability == AvailabilityStatus.Available
                    && file.MediaSource.IsEnabled
                    && file.MediaSource.Availability == AvailabilityStatus.Available
                    && file.ProbeFingerprint == null
                    && file.LastErrorCode == null)
                .OrderBy(file => file.Id))
            .Include(file => file.MediaSource)
            .FirstOrDefaultAsync(cancellationToken);
        if (media is null) return false;

        var result = await mediaProbe.ProbeAsync(Path.Combine(media.MediaSource.RootPath, media.RelativePath), cancellationToken);
        if (result.IsSuccess)
        {
            media.DurationSeconds = result.Value.DurationSeconds;
            media.ProbeFingerprint = MediaScanService.Fingerprint(media.MediaSource, media);
            media.LastErrorCode = null;
        }
        else
        {
            // Keep the index and allow queue-head playback to retry; skip this failed item in this preflight pass.
            media.LastErrorCode = result.Error.Code;
        }
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
