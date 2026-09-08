using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfPlaybackFailureStore(StationDbContext database) : IPlaybackFailureStore
{
    public async Task RecordAsync(
        PlaybackError error,
        AvailabilityStatus? mediaAvailability,
        CancellationToken cancellationToken = default)
    {
        database.PlaybackErrors.Add(error);
        if (error.MediaFileId is { } mediaFileId && mediaAvailability is { } availability)
        {
            var media = await database.MediaFiles.SingleOrDefaultAsync(x => x.Id == mediaFileId, cancellationToken).ConfigureAwait(false);
            if (media is not null)
            {
                media.Availability = availability;
                media.LastErrorCode = error.ErrorCode;
                if (availability == AvailabilityStatus.Offline)
                {
                    var song = await database.Songs.SingleOrDefaultAsync(x => x.Id == media.SongId, cancellationToken).ConfigureAwait(false);
                    if (song is not null)
                    {
                        var hasAvailableAlternative = await database.MediaFiles.AnyAsync(
                            x => x.SongId == media.SongId && x.Id != media.Id && x.Availability == AvailabilityStatus.Available,
                            cancellationToken).ConfigureAwait(false);
                        song.Availability = hasAvailableAlternative ? AvailabilityStatus.Available : AvailabilityStatus.Offline;
                    }
                }
            }
        }
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
