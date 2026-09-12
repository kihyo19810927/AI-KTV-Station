using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Search;

namespace Station.Infrastructure.Persistence;

public sealed record DatabaseUpgradeResult(IReadOnlyList<string> PendingMigrations, string? BackupPath, bool Migrated);

public interface IDatabaseMigrationExecutor
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(StationDbContext database, CancellationToken cancellationToken);
    Task MigrateAsync(StationDbContext database, CancellationToken cancellationToken);
}

public sealed class EfDatabaseMigrationExecutor : IDatabaseMigrationExecutor
{
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(StationDbContext database, CancellationToken cancellationToken) =>
        (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

    public Task MigrateAsync(StationDbContext database, CancellationToken cancellationToken) =>
        database.Database.MigrateAsync(cancellationToken);
}

public sealed class DatabaseUpgradeException(string backupPath, Exception innerException)
    : Exception("Database migration failed and the pre-migration backup was restored.", innerException)
{
    public string BackupPath { get; } = backupPath;
}

public sealed class DatabaseUpgradeService(
    StationDbContext database,
    IDatabaseMigrationExecutor migrationExecutor,
    TimeProvider timeProvider,
    ISongSearchIndex? searchIndex = null)
{
    public async Task<DatabaseUpgradeResult> UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var pending = await migrationExecutor.GetPendingMigrationsAsync(database, cancellationToken);
        if (pending.Count == 0) return new(pending, null, false);

        var databasePath = Path.GetFullPath(database.Database.GetDbConnection().DataSource);
        string? backupPath = null;
        if (File.Exists(databasePath) && new FileInfo(databasePath).Length > 0)
            backupPath = await CreateBackupAsync(databasePath, cancellationToken);

        try
        {
            await migrationExecutor.MigrateAsync(database, cancellationToken);
            if (searchIndex is not null) await searchIndex.RebuildAsync(cancellationToken);
            return new(pending, backupPath, true);
        }
        catch (Exception migrationError) when (backupPath is not null)
        {
            await database.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
            await RestoreBackupAsync(backupPath, databasePath, cancellationToken);
            throw new DatabaseUpgradeException(backupPath, migrationError);
        }
    }

    private async Task<string> CreateBackupAsync(string databasePath, CancellationToken cancellationToken)
    {
        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);
        var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(backupDirectory, $"station-before-migration-{stamp}-{Guid.NewGuid():N}.db");
        await BackupAsync(databasePath, backupPath, cancellationToken);
        await VerifyIntegrityAsync(backupPath, cancellationToken);
        return backupPath;
    }

    private static async Task BackupAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly");
        await using var destination = new SqliteConnection($"Data Source={destinationPath};Mode=ReadWriteCreate");
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task RestoreBackupAsync(string backupPath, string databasePath, CancellationToken cancellationToken)
    {
        await using var backup = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
        await using var destination = new SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
        await backup.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        backup.BackupDatabase(destination);
        await VerifyIntegrityAsync(databasePath, cancellationToken);
    }

    private static async Task VerifyIntegrityAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SQLite backup integrity check failed.");
    }
}
