using Station.Application.Common;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;

namespace Station.Application.Catalog;

public interface ICatalogScanService
{
    Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class CatalogScanService(IMediaScanRunner scanner, ISongSearchIndex search) : ICatalogScanService
{
    public async Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (mediaSourceId == Guid.Empty) return Result<ScanRun>.Failure(new Error("scan.invalid_source_id", "Media source id is required."));
        var result = await scanner.ScanAsync(mediaSourceId, Guid.NewGuid(), progress ?? new Progress<MediaScanProgress>(), cancellationToken);
        if (result.IsSuccess && result.Value.Status == ScanStatus.Completed) await search.RebuildAsync(cancellationToken);
        return result;
    }
}
