using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Infrastructure.Health;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class StationHealthServiceTests
{
    [Fact]
    public async Task Empty_reachable_database_reports_database_healthy_and_source_warning()
    {
        var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var database = new StationDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();
        var service = new StationHealthService(database, new StationOptions { Player = new PlayerOptions { ExecutablePath = "missing-mpv.exe" } }, TimeProvider.System);

        var snapshot = await service.CheckAsync();

        Assert.Contains(snapshot.Components, x => x.Name == "本地数据库" && x.Level == HealthLevel.Healthy);
        Assert.Contains(snapshot.Components, x => x.Name == "媒体挂载" && x.Level == HealthLevel.Warning);
        Assert.Contains(snapshot.Components, x => x.Name == "mpv 播放器" && x.Level == HealthLevel.Unavailable);
    }
}
