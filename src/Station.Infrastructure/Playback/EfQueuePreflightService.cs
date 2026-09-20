using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Media;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfQueuePreflightService(StationDbContext database, IMediaProbe mediaProbe, IQueueStatusNotifier? notifier = null, ILocalDiagnosticLog? diagnosticLog = null) : IQueuePreflightService
{
    public async Task<bool> ProbeNextWaitingAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var item = await database.QueueItems
            .Include(item => item.Song).ThenInclude(song => song.MediaFiles).ThenInclude(file => file.MediaSource)
            .Where(item => item.RoomSessionId == roomId && item.Status == QueueItemStatus.Probing)
            .OrderBy(item => item.Position)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return false;
        var onlineMedia = item.Song.MediaFiles.Where(file => file.MediaSource.IsEnabled &&
                file.MediaSource.Availability == AvailabilityStatus.Available)
            .OrderBy(file => file.Id).ToArray();
        var media = onlineMedia.FirstOrDefault(file => file.Availability == AvailabilityStatus.Available)
            ?? onlineMedia.FirstOrDefault(file => file.Availability == AvailabilityStatus.Unreadable);
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
        var result = Result<MediaProbeResult>.Failure(new Error("media_probe.not_started", "Media probe did not start."));
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            result = await mediaProbe.ProbeAsync(Path.Combine(media.MediaSource.RootPath, media.RelativePath), cancellationToken);
            if (result.IsSuccess) break;
        }
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
            // ffprobe is advisory for mounted/cloud media. A transient probe
            // failure must not prevent mpv from attempting the real playback.
            item.Status = QueueItemStatus.Waiting;
            if (diagnosticLog is not null)
                await diagnosticLog.WriteAsync("Warning", "queue.preflight_probe_failed",
                    $"Queue item {item.Id} will still be handed to the player after three probe attempts: {result.Error.Code}; {result.Error.Message}", cancellationToken);
        }
        await database.SaveChangesAsync(cancellationToken);
        if (notifier is not null) await notifier.NotifyAsync(roomId, item.Id, item.Status, cancellationToken);
        return true;
    }
}
