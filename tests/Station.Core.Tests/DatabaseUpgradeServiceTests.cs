using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class DatabaseUpgradeServiceTests
{
    [Fact]
    public async Task Existing_database_is_backed_up_before_upgrade()
    {
        var fixture = await DatabaseFixture.CreateAsync();
        try
        {
            await using var database = fixture.Open();
            var service = new DatabaseUpgradeService(database, new ControlledMigrationExecutor(), TimeProvider.System);

            var result = await service.UpgradeAsync();

            Assert.True(result.Migrated);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            await AssertIntegrityAsync(result.BackupPath!);
        }
        finally { fixture.Dispose(); }
    }

    [Fact]
    public async Task Failed_upgrade_restores_catalog_from_consistent_backup()
    {
        var fixture = await DatabaseFixture.CreateAsync();
        try
        {
            string backupPath;
            await using (var database = fixture.Open())
            {
                var service = new DatabaseUpgradeService(database, new ControlledMigrationExecutor(failAfterDeletingSongs: true), TimeProvider.System);
                var error = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => service.UpgradeAsync());
                backupPath = error.BackupPath;
            }

            await using var restored = fixture.Open();
            Assert.Equal("保留歌曲", (await restored.Songs.SingleAsync()).Title);
            Assert.True(File.Exists(backupPath));
            await AssertIntegrityAsync(fixture.Path);
        }
        finally { fixture.Dispose(); }
    }

    [Fact]
    public async Task Current_database_does_not_create_redundant_backup()
    {
        var fixture = await DatabaseFixture.CreateAsync();
        try
        {
            await using var database = fixture.Open();
            var service = new DatabaseUpgradeService(database, new ControlledMigrationExecutor(hasPendingMigration: false), TimeProvider.System);
            var result = await service.UpgradeAsync();
            Assert.False(result.Migrated);
            Assert.Null(result.BackupPath);
            Assert.False(Directory.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fixture.Path)!, "backups")));
        }
        finally { fixture.Dispose(); }
    }

    private static async Task AssertIntegrityAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", await command.ExecuteScalarAsync());
    }

    private sealed class ControlledMigrationExecutor(bool hasPendingMigration = true, bool failAfterDeletingSongs = false) : IDatabaseMigrationExecutor
    {
        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(StationDbContext database, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(hasPendingMigration ? ["controlled"] : []);

        public async Task MigrateAsync(StationDbContext database, CancellationToken cancellationToken)
        {
            if (!failAfterDeletingSongs) return;
            await database.Database.ExecuteSqlRawAsync("DELETE FROM Songs", cancellationToken);
            throw new InvalidOperationException("Injected migration failure.");
        }
    }

    private sealed class DatabaseFixture(string root, DbContextOptions<StationDbContext> options) : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(root, "station.db");
        public StationDbContext Open() => new(options);

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-ktv-db-upgrade-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var path = System.IO.Path.Combine(root, "station.db");
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var database = new StationDbContext(options);
            await database.Database.MigrateAsync();
            database.Songs.Add(new Song { Title = "保留歌曲", NormalizedTitle = "保留歌曲", Availability = AvailabilityStatus.Available });
            await database.SaveChangesAsync();
            return new(root, options);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
