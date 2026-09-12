using System.Windows.Input;
using Station.Application.Configuration;

namespace Station.Desktop.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IStationSettingsStore store;
    private readonly IDiagnosticExportService diagnostics;
    private readonly ILocalDiagnosticLog log;
    private string bindAddress;
    private int port;
    private string dataDirectory;
    private int commandTimeoutSeconds;
    private string statusMessage = "设置保存在当前 Windows 用户目录";
    private StationOptions current;

    public SettingsViewModel(IStationSettingsStore store, IDiagnosticExportService diagnostics, ILocalDiagnosticLog log, StationOptions options)
    {
        this.store = store; this.diagnostics = diagnostics; this.log = log; current = options;
        bindAddress = options.Server.BindAddress; port = options.Server.Port; dataDirectory = options.Storage.DataDirectory; commandTimeoutSeconds = options.Player.CommandTimeoutSeconds;
        SaveCommand = new AsyncRelayCommand(SaveAsync); ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync);
    }

    public ICommand SaveCommand { get; }
    public ICommand ExportDiagnosticsCommand { get; }
    public string BindAddress { get => bindAddress; set => SetProperty(ref bindAddress, value); }
    public int Port { get => port; set => SetProperty(ref port, value); }
    public string DataDirectory { get => dataDirectory; set => SetProperty(ref dataDirectory, value); }
    public int CommandTimeoutSeconds { get => commandTimeoutSeconds; set => SetProperty(ref commandTimeoutSeconds, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }

    public async Task SaveAsync()
    {
        var next = new StationOptions { Server = new() { BindAddress = BindAddress, Port = Port }, Storage = new() { DataDirectory = DataDirectory }, Player = new() { CommandTimeoutSeconds = CommandTimeoutSeconds }, Scanning = current.Scanning };
        var result = await store.SaveAsync(next);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        current = next; await log.WriteAsync("Information", "settings.saved", "Local settings were saved; restart may be required."); StatusMessage = "设置已保存；端口、目录或播放器变更将在重启后生效";
    }

    public async Task ExportDiagnosticsAsync()
    {
        var result = await diagnostics.ExportAsync(current);
        if (result.IsSuccess) await log.WriteAsync("Information", "diagnostics.exported", "A redacted diagnostic report was exported.");
        StatusMessage = result.IsSuccess ? $"诊断报告已导出：{result.Value.FileName}（{result.Value.SizeBytes} 字节）" : result.Error.Message;
    }
}
