using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class LegacyQueueLimitMigrationTests
{
    [Fact]
    public async Task Upgrades_the_previous_default_of_ten_without_recreating_the_database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-limit-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var database = new StationDbContext(options);
            Assert.Contains("20260911150500_UpgradeLegacyQueueLimit", database.Database.GetMigrations());
            var migrator = database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260910105525_AddProbeCheckpoint");
            database.RoomSessions.Add(new RoomSession { JoinCode = "ABC234", CreatedAt = DateTimeOffset.UtcNow, MaxQueuedSongsPerGuest = 10 });
            await database.SaveChangesAsync();

            await migrator.MigrateAsync();

            Assert.Equal(100, (await database.RoomSessions.AsNoTracking().SingleAsync()).MaxQueuedSongsPerGuest);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
