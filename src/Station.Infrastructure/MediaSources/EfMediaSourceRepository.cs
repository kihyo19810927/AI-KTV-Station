using Microsoft.EntityFrameworkCore;
using Station.Application.MediaSources;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.MediaSources;

public sealed class EfMediaSourceRepository(StationDbContext database) : IMediaSourceRepository
{
    public Task<MediaSource?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.MediaSources.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<MediaSource?> FindByRootPathAsync(string canonicalPath, CancellationToken cancellationToken = default) =>
        database.MediaSources.SingleOrDefaultAsync(x => x.RootPath == canonicalPath, cancellationToken);

    public async Task<IReadOnlyList<MediaSource>> ListAsync(CancellationToken cancellationToken = default) =>
        await database.MediaSources.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task AddAsync(MediaSource source, CancellationToken cancellationToken = default) =>
        database.MediaSources.AddAsync(source, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
