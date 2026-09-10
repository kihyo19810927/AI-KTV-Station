using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IMediaScanRunner
{
    Task<Result<ScanRun>> ScanAsync(
        Guid mediaSourceId,
        Guid scanRunId,
        IProgress<MediaScanProgress> progress,
        CancellationToken cancellationToken = default);
}

public sealed record MediaScanProgress(
    Guid ScanRunId,
    ScanStatus Status,
    long DiscoveredFiles,
    long UpdatedFiles,
    long ErrorCount,
    long IndexedFiles = 0,
    long ProbedFiles = 0,
    long CachedFiles = 0,
    double AverageProbeMilliseconds = 0,
    string Phase = "Indexing");
