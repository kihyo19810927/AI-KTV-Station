using System.Text.Json;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Health;

namespace Station.Infrastructure.Configuration;

public sealed class JsonDiagnosticExportService(string exportDirectory, IStationHealthService health, ILocalDiagnosticLog log) : IDiagnosticExportService
{
    public async Task<Result<DiagnosticExportResult>> ExportAsync(StationOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(exportDirectory);
            var snapshot = await health.CheckAsync(cancellationToken);
            var events = await log.ReadRecentAsync(200, cancellationToken);
            var document = new
            {
                SchemaVersion = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                Runtime = new { Framework = Environment.Version.ToString(), OS = Environment.OSVersion.VersionString, ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() },
                Configuration = new { options.Server.BindAddress, options.Server.Port, DataDirectoryConfigured = !string.IsNullOrWhiteSpace(options.Storage.DataDirectory), PlayerExecutableConfigured = !string.IsNullOrWhiteSpace(options.Player.ExecutablePath), options.Player.CommandTimeoutSeconds },
                Health = snapshot.Components.Select(x => new { x.Name, Level = x.Level.ToString(), x.Summary }),
                Recovery = snapshot.Components.Where(x => x.Level != HealthLevel.Healthy).Select(RecoverySuggestion).Distinct().ToArray(),
                Events = events,
            };
            var fileName = $"station-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
            var destination = Path.Combine(exportDirectory, fileName);
            await using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                await JsonSerializer.SerializeAsync(stream, document, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
            return Result<DiagnosticExportResult>.Success(new(fileName, new FileInfo(destination).Length));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<DiagnosticExportResult>.Failure(new Error("diagnostics.export_failed", "The diagnostic report could not be exported."));
        }
    }

    private static string RecoverySuggestion(HealthComponent component) => component.Name switch
    {
        "本地数据库" => "检查数据目录权限；恢复前先备份现有数据库，禁止删库重建。",
        "媒体挂载" => "确认 CloudDrive 挂载在线；恢复后重新扫描所选来源，离线索引会保留。",
        "mpv 播放器" => "检查 mpv 配置或 WinGet 安装状态，然后重启主控。",
        _ => "重启主控后再次运行健康检查；若仍失败，导出新的诊断报告。",
    };
}
