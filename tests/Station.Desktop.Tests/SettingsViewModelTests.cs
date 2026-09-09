using Station.Application.Common;
using Station.Application.Configuration;
using Station.Desktop.ViewModels;

namespace Station.Desktop.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task Save_validates_through_store_and_reports_restart_boundary()
    {
        var store = new FakeStore(); var log = new FakeLog();
        var viewModel = new SettingsViewModel(store, new FakeDiagnostics(), log, new StationOptions()) { Port = 5098, DataDirectory = "new-data" };
        await viewModel.SaveAsync();
        Assert.Equal(5098, store.Saved!.Server.Port); Assert.Contains("重启后生效", viewModel.StatusMessage);
        Assert.Contains("settings.saved", log.Codes);
    }

    private sealed class FakeStore : IStationSettingsStore
    {
        public StationOptions? Saved { get; private set; }
        public Task<Result<StationOptions>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<StationOptions>.Success(new()));
        public Task<Result<bool>> SaveAsync(StationOptions options, CancellationToken cancellationToken = default) { Saved = options; return Task.FromResult(Result<bool>.Success(true)); }
    }
    private sealed class FakeDiagnostics : IDiagnosticExportService
    {
        public Task<Result<DiagnosticExportResult>> ExportAsync(StationOptions options, CancellationToken cancellationToken = default) => Task.FromResult(Result<DiagnosticExportResult>.Success(new("report.json", 10)));
    }
    private sealed class FakeLog : ILocalDiagnosticLog
    {
        public List<string> Codes { get; } = [];
        public Task WriteAsync(string level, string code, string message, CancellationToken cancellationToken = default) { Codes.Add(code); return Task.CompletedTask; }
        public Task<IReadOnlyList<DiagnosticLogEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DiagnosticLogEntry>>([]);
    }
}
