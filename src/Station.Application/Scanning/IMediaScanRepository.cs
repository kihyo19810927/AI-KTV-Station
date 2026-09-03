using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IMediaScanRepository
{
    Task<MediaSource?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaFile>> ListFilesAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task AddFileAsync(MediaFile file, CancellationToken cancellationToken = default);
    Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
