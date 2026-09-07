using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IScanRunReader
{
    Task<ScanRun?> FindAsync(Guid scanRunId, CancellationToken cancellationToken = default);
}
