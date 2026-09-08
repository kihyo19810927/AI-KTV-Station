using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfTrackMappingRepository(StationDbContext database) : ITrackMappingRepository
{
    public Task<TrackMapping?> FindAsync(Guid mediaFileId, CancellationToken cancellationToken = default) =>
        database.TrackMappings.AsNoTracking().SingleOrDefaultAsync(x => x.MediaFileId == mediaFileId, cancellationToken);

    public async Task SaveAsync(TrackMapping mapping, CancellationToken cancellationToken = default)
    {
        var stored = await database.TrackMappings.SingleOrDefaultAsync(x => x.MediaFileId == mapping.MediaFileId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            database.TrackMappings.Add(mapping);
        }
        else
        {
            stored.BackingTrackId = mapping.BackingTrackId;
            stored.VocalTrackId = mapping.VocalTrackId;
            stored.DefaultSubtitleTrackId = mapping.DefaultSubtitleTrackId;
            stored.IsManualOverride = mapping.IsManualOverride;
        }
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
