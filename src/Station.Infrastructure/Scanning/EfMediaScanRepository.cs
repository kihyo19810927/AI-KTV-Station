using Microsoft.EntityFrameworkCore;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Scanning;

public sealed class EfMediaScanRepository(StationDbContext database) : IMediaScanRepository
{
    public Task<MediaSource?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        database.MediaSources.SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken);

    public async Task<IReadOnlyList<MediaFile>> ListFilesAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        await database.MediaFiles
            .Include(x => x.Tracks)
            .Include(x => x.Song).ThenInclude(x => x.Artists).ThenInclude(x => x.Artist)
            .Where(x => x.MediaSourceId == sourceId)
            .ToListAsync(cancellationToken);

    public Task AddFileAsync(MediaFile file, CancellationToken cancellationToken = default) => database.MediaFiles.AddAsync(file, cancellationToken).AsTask();
    public Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default) => database.ScanRuns.AddAsync(run, cancellationToken).AsTask();
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
