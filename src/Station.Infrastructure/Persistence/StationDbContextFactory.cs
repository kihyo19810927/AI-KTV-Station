using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Station.Infrastructure.Persistence;

public sealed class StationDbContextFactory : IDesignTimeDbContextFactory<StationDbContext>
{
    public StationDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "ai-ktv-station-design.db");
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        return new StationDbContext(options);
    }
}
