using Station.Domain.Models;

namespace Station.Application.MediaSources;

public interface IMediaSourceRepository
{
    Task<MediaSource?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaSource?> FindByRootPathAsync(string canonicalPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaSource>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MediaSource source, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
