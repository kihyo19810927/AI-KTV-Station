using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IScanCoordinator
{
    Result<ScanOperationStatus> Start(Guid mediaSourceId);
    Result<ScanOperationStatus> Get(Guid scanRunId);
    Result<ScanOperationStatus> Cancel(Guid scanRunId);
}

public sealed record ScanOperationStatus(
    Guid ScanRunId,
    Guid MediaSourceId,
    ScanStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    long DiscoveredFiles,
    long UpdatedFiles,
    long ErrorCount,
    string? ErrorCode);
