using System.Text.Json;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Infrastructure.Configuration;

namespace Station.Core.Tests;

public sealed class StationSettingsTests
{
    [Fact]
    public async Task Settings_round_trip_validated_values_and_reject_invalid_port()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-settings-{Guid.NewGuid():N}"); var file = Path.Combine(root, "settings.json");
        try
        {
            var store = new JsonStationSettingsStore(file);
            var options = new StationOptions { Server = new() { BindAddress = "0.0.0.0", Port = 5091 }, Storage = new() { DataDirectory = "station-data" }, Player = new() { ExecutablePath = "mpv.exe", CommandTimeoutSeconds = 15 } };
            Assert.True((await store.SaveAsync(options)).IsSuccess);
            var loaded = await store.LoadAsync(); Assert.True(loaded.IsSuccess); Assert.Equal(5091, loaded.Value.Server.Port); Assert.Equal("station-data", loaded.Value.Storage.DataDirectory);
            Assert.Equal("configuration.invalid", (await store.SaveAsync(new StationOptions { Server = new() { Port = 0 } })).Error.Code);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Diagnostic_export_contains_recovery_but_not_private_paths_or_credentials()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-diagnostics-{Guid.NewGuid():N}");
        try
        {
            var log = new JsonLineDiagnosticLog(Path.Combine(root, "station.jsonl"), TimeProvider.System);
            await log.WriteAsync("Information", "scan.completed", "Selected media source scan completed.");
            var service = new JsonDiagnosticExportService(root, new FakeHealth(), log);
            var options = new StationOptions { Storage = new() { DataDirectory = @"E:\private-library" }, Player = new() { ExecutablePath = @"C:\private\mpv.exe" } };
            var result = await service.ExportAsync(options);
            Assert.True(result.IsSuccess); Assert.Equal(Path.GetFileName(result.Value.FileName), result.Value.FileName);
            var json = await File.ReadAllTextAsync(Path.Combine(root, result.Value.FileName));
            Assert.DoesNotContain("private-library", json); Assert.DoesNotContain("private\\mpv", json); Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
            using var document = JsonDocument.Parse(json);
            Assert.True(document.RootElement.TryGetProperty("Recovery", out _));
            Assert.Equal("scan.completed", document.RootElement.GetProperty("Events")[0].GetProperty("Code").GetString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Diagnostic_log_sanitizes_lines_and_returns_only_requested_recent_entries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-log-{Guid.NewGuid():N}");
        try
        {
            var log = new JsonLineDiagnosticLog(Path.Combine(root, "station.jsonl"), TimeProvider.System);
            await log.WriteAsync("Information", "first", "line one");
            await log.WriteAsync("Warning", "second", "line two\r\ncontinued");
            var entries = await log.ReadRecentAsync(1);
            Assert.Single(entries);
            Assert.Equal("second", entries[0].Code);
            Assert.DoesNotContain('\r', entries[0].Message);
            Assert.DoesNotContain('\n', entries[0].Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FakeHealth : IStationHealthService
    {
        public Task<StationHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(new StationHealthSnapshot(DateTimeOffset.UtcNow, [new("媒体挂载", HealthLevel.Warning, "尚未配置媒体源")]));
    }
}
