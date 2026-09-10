using Microsoft.Extensions.DependencyInjection;
using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;

namespace Station.Desktop.Services;

public sealed class BackgroundCatalogScanService(IServiceScopeFactory scopes) : ICatalogScanService
{
    public Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IMediaScanRunner>();
            var search = scope.ServiceProvider.GetRequiredService<ISongSearchIndex>();
            return await new CatalogScanService(scanner, search).ScanAsync(mediaSourceId, progress, cancellationToken);
        }, CancellationToken.None);
}
