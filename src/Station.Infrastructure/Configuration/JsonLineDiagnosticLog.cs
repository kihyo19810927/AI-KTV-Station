using System.Text.Json;
using Station.Application.Configuration;

namespace Station.Infrastructure.Configuration;

public sealed class JsonLineDiagnosticLog(string filePath, TimeProvider clock) : ILocalDiagnosticLog
{
    private const long MaximumBytes = 1024 * 1024;
    private const int RetainedEntries = 1000;
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task WriteAsync(string level, string code, string message, CancellationToken cancellationToken = default)
    {
        var entry = new DiagnosticLogEntry(clock.GetUtcNow(), Safe(level, 20), Safe(code, 100), Safe(message, 300));
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.AppendAllTextAsync(filePath, JsonSerializer.Serialize(entry) + Environment.NewLine, cancellationToken);
            if (new FileInfo(filePath).Length > MaximumBytes)
            {
                var retained = (await File.ReadAllLinesAsync(filePath, cancellationToken)).TakeLast(RetainedEntries);
                await File.WriteAllLinesAsync(filePath, retained, cancellationToken);
            }
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<DiagnosticLogEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(count));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath)) return [];
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            return lines.TakeLast(count).Select(x => JsonSerializer.Deserialize<DiagnosticLogEntry>(x)).Where(x => x is not null).Cast<DiagnosticLogEntry>().ToArray();
        }
        finally { gate.Release(); }
    }

    private static string Safe(string value, int maxLength)
    {
        var sanitized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }
}
