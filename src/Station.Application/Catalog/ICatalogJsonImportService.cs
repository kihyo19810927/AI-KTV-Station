using Station.Application.Common;

namespace Station.Application.Catalog;

public sealed record CatalogImportProgress(long Read, long Added, long Skipped, long Errors);
public sealed record CatalogImportResult(long Read, long Added, long Skipped, long Errors);

public interface ICatalogJsonImportService
{
    Task<Result<CatalogImportResult>> ImportAsync(string indexPath, string mountRoot,
        IProgress<CatalogImportProgress>? progress = null, CancellationToken cancellationToken = default);
}
