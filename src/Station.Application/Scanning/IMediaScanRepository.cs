using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IMediaScanRepository
{
    Task<IReadOnlyList<ArtistIdentity>> ListArtistsAsync(CancellationToken cancellationToken = default);
    Task<MediaSource?> FindSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaFile>> ListFilesAsync(Guid sourceId, CancellationToken cancellationToken = default);
    Task AddFileAsync(MediaFile file, CancellationToken cancellationToken = default);
    Task AddTrackAsync(MediaTrack track, CancellationToken cancellationToken = default);
    Task AddRunAsync(ScanRun run, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record ArtistIdentity(Guid Id, string NormalizedName);
