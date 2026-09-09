using Station.Application.Common;

namespace Station.Application.Configuration;

public interface IStationSettingsStore
{
    Task<Result<StationOptions>> LoadAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> SaveAsync(StationOptions options, CancellationToken cancellationToken = default);
}

public sealed record DiagnosticExportResult(string FileName, long SizeBytes);
public interface IDiagnosticExportService
{
    Task<Result<DiagnosticExportResult>> ExportAsync(StationOptions options, CancellationToken cancellationToken = default);
}

public sealed record DiagnosticLogEntry(DateTimeOffset Timestamp, string Level, string Code, string Message);
public interface ILocalDiagnosticLog
{
    Task WriteAsync(string level, string code, string message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiagnosticLogEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default);
}
