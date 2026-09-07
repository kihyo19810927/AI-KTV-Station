using Microsoft.EntityFrameworkCore;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Scanning;

public sealed class EfScanRunReader(StationDbContext database) : IScanRunReader
{
    public Task<ScanRun?> FindAsync(Guid scanRunId, CancellationToken cancellationToken = default) =>
        database.ScanRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == scanRunId, cancellationToken);
}
