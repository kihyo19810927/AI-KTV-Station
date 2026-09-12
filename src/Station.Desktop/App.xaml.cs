using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Station.Desktop.ViewModels;
using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Infrastructure.Health;
using Station.Infrastructure.Persistence;
using Station.Application.Playback;
using Station.Infrastructure.Playback;
using Station.Application.Queue;
using Station.Desktop.Services;
using Station.Infrastructure.Queue;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using Station.Application.Catalog;
using Station.Application.Media;
using Station.Application.MediaSources;
using Station.Application.Metadata;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Infrastructure.Catalog;
using Station.Infrastructure.Media;
using Station.Infrastructure.MediaSources;
using Station.Infrastructure.Runtime;
using Station.Infrastructure.Metadata;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;
using Station.Application.Rooms;
using Station.Infrastructure.Rooms;
using Station.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Station.Server.Hosting;

namespace Station.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;
    private WebApplication? embeddedServer;
    private System.Windows.Threading.DispatcherTimer? refreshTimer;
    private System.Windows.Threading.DispatcherTimer? progressTimer;
    private bool refreshing;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        try { await StartDesktopAsync(); }
        catch (Exception exception)
        {
            var message = exception.ToString().Contains("AddressInUse", StringComparison.OrdinalIgnoreCase)
                ? "点歌服务端口被占用。请关闭旧版 Station 后重试，或在本机设置中更换端口。"
                : $"启动未完成（{exception.GetType().Name}）。请保留数据库并查看本机启动错误记录。";
            try
            {
                var root = Environment.GetEnvironmentVariable("AI_KTV_STATION_SETTINGS_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-KTV Station");
                Directory.CreateDirectory(root);
                await File.WriteAllTextAsync(Path.Combine(root, "startup-error.txt"), exception.ToString());
            }
            catch (IOException) { }
            MessageBox.Show(message, "AI-KTV Station 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            await DisposeResourcesAsync();
            Shutdown(1);
        }
    }

    private async Task StartDesktopAsync()
    {
        var collection = new ServiceCollection();
        var settingsRoot = Environment.GetEnvironmentVariable("AI_KTV_STATION_SETTINGS_ROOT");
        if (string.IsNullOrWhiteSpace(settingsRoot))
            settingsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-KTV Station");
        settingsRoot = Path.GetFullPath(settingsRoot);
        var settingsStore = new JsonStationSettingsStore(Path.Combine(settingsRoot, "settings.json"));
        var loadedSettings = await settingsStore.LoadAsync();
        var options = loadedSettings.IsSuccess ? loadedSettings.Value : new StationOptions();
        if (options.Server.BindAddress == "127.0.0.1")
        {
            options = new StationOptions
            {
                Server = new ServerOptions { BindAddress = "0.0.0.0", Port = options.Server.Port },
                Storage = options.Storage,
                Player = options.Player,
                Scanning = options.Scanning,
            };
            await settingsStore.SaveAsync(options);
        }
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, options.Storage.DataDirectory);
        Directory.CreateDirectory(dataDirectory);
        collection.AddSingleton(options);
        collection.AddSingleton(options.Scanning);
        collection.AddSingleton<IStationSettingsStore>(settingsStore);
        collection.AddSingleton(TimeProvider.System);
        var databaseOptions = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={Path.Combine(dataDirectory, "station.db")}").Options;
        collection.AddSingleton(new StationDbContext(databaseOptions));
        collection.AddSingleton<IDatabaseMigrationExecutor, EfDatabaseMigrationExecutor>();
        collection.AddSingleton<DatabaseUpgradeService>();
        collection.AddSingleton<IStationHealthService, StationHealthService>();
        collection.AddSingleton<IPlayerAdapter>(_ => new MpvPlayerAdapter(new PlayerOptions
        {
            ExecutablePath = ExternalToolLocator.Find("mpv.exe") ?? "mpv.exe",
            CommandTimeoutSeconds = options.Player.CommandTimeoutSeconds,
        }));
        collection.AddSingleton<PlaybackControlService>();
        collection.AddSingleton<IPlaybackStartupRecoveryStore, EfPlaybackStartupRecoveryStore>();
        collection.AddSingleton<PlaybackStartupRecoveryService>();
        collection.AddSingleton<IRoomQueueRepository, EfRoomQueueRepository>();
        collection.AddSingleton<IRoomQueueLock, InProcessRoomQueueLock>();
        collection.AddSingleton<IRoomQueueService, RoomQueueService>();
        collection.AddSingleton<HostRoomContext>();
        collection.AddSingleton<PlaybackConsoleViewModel>(provider => new PlaybackConsoleViewModel(
            provider.GetRequiredService<PlaybackControlService>(),
            provider.GetRequiredService<IRoomQueueService>(),
            provider.GetRequiredService<HostRoomContext>()));
        collection.AddSingleton<QueueManagementViewModel>();
        collection.AddSingleton<IMediaSourceRepository, EfMediaSourceRepository>();
        collection.AddSingleton<IMediaPathInspector, FileSystemMediaPathInspector>();
        collection.AddSingleton<IMediaSourceService, MediaSourceService>();
        collection.AddSingleton<ISearchTextNormalizer, ToolGoodSearchTextNormalizer>();
        collection.AddSingleton<ISongSearchIndex, SqliteSongSearchIndex>();
        collection.AddSingleton<ICatalogAdminRepository, EfCatalogAdminRepository>();
        collection.AddSingleton<ICatalogAdminService, CatalogAdminService>();
        collection.AddSingleton<ICatalogJsonImportService, CatalogJsonImportService>();
        collection.AddSingleton<Station.Desktop.Services.ICatalogImportFilePicker, Station.Desktop.Services.CatalogImportFilePicker>();
        collection.AddSingleton<IMediaScanRepository, EfMediaScanRepository>();
        collection.AddSingleton<IMediaFileEnumerator, FileSystemMediaFileEnumerator>();
        collection.AddSingleton<IMediaFilenameParser, KtvFilenameParser>();
        collection.AddSingleton<INfoMetadataReader, NfoXmlMetadataReader>();
        collection.AddSingleton<IMediaProbe>(_ => new FfprobeMediaProbe(ExternalToolLocator.Find("ffprobe.exe") ?? "ffprobe.exe", TimeSpan.FromSeconds(30)));
        collection.AddSingleton<IMediaScanRunner, MediaScanService>();
        collection.AddSingleton<ICatalogScanService>(_ => new BackgroundCatalogScanService(embeddedServer!.Services.GetRequiredService<IServiceScopeFactory>()));
        collection.AddSingleton<CatalogManagementViewModel>();
        collection.AddSingleton<IRoomRepository, EfRoomRepository>();
        collection.AddSingleton<IRoomJoinCodeGenerator, SecureRoomJoinCodeGenerator>();
        collection.AddSingleton<RoomLifecycleService>();
        collection.AddSingleton<IRoomIdentityRepository, EfRoomIdentityRepository>();
        collection.AddSingleton<IRoomTokenProtector, Sha256RoomTokenProtector>();
        collection.AddSingleton<RoomAuthenticationService>();
        collection.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
        collection.AddSingleton<ILanAddressProvider, LanAddressProvider>();
        collection.AddSingleton<RoomManagementViewModel>();
        collection.AddSingleton<ILocalDiagnosticLog>(_ => new JsonLineDiagnosticLog(Path.Combine(settingsRoot, "logs", "station.jsonl"), TimeProvider.System));
        collection.AddSingleton<IDiagnosticExportService>(provider => new JsonDiagnosticExportService(Path.Combine(settingsRoot, "diagnostics"), provider.GetRequiredService<IStationHealthService>(), provider.GetRequiredService<ILocalDiagnosticLog>()));
        collection.AddSingleton<SettingsViewModel>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await services.GetRequiredService<ILocalDiagnosticLog>().WriteAsync("Information", "desktop.starting", "AI-KTV Station desktop is starting.");
        await services.GetRequiredService<DatabaseUpgradeService>().UpgradeAsync();
        await services.GetRequiredService<PlaybackStartupRecoveryService>().RecoverAsync();
        var serverOptions = new StationOptions
        {
            Server = options.Server,
            Storage = new StorageOptions { DataDirectory = dataDirectory },
            Player = options.Player,
        };
        embeddedServer = StationServerHost.Build([], builder =>
        {
            builder.Services.AddSingleton(options.Scanning);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{StationOptions.SectionName}:Server:BindAddress"] = serverOptions.Server.BindAddress,
                [$"{StationOptions.SectionName}:Server:Port"] = serverOptions.Server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [$"{StationOptions.SectionName}:Storage:DataDirectory"] = serverOptions.Storage.DataDirectory,
                [$"{StationOptions.SectionName}:Player:CommandTimeoutSeconds"] = serverOptions.Player.CommandTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }, services.GetRequiredService<IPlayerAdapter>(), Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        await StationServerHost.InitializeAsync(embeddedServer.Services);
        await embeddedServer.StartAsync();
        await services.GetRequiredService<ILocalDiagnosticLog>().WriteAsync("Information", "server.started", "The embedded room service started.");
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        await services.GetRequiredService<MainWindowViewModel>().RefreshHealthAsync();
        await services.GetRequiredService<CatalogManagementViewModel>().InitializeAsync();
        await services.GetRequiredService<RoomManagementViewModel>().RefreshAsync();
        refreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        refreshTimer.Tick += async (_, _) =>
        {
            if (refreshing || services is null) return;
            refreshing = true;
            try
            {
                await services.GetRequiredService<PlaybackConsoleViewModel>().RefreshAsync();
                await services.GetRequiredService<QueueManagementViewModel>().RefreshAsync();
            }
            finally { refreshing = false; }
        };
        refreshTimer.Start();
        progressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        progressTimer.Tick += (_, _) => services?.GetRequiredService<PlaybackConsoleViewModel>().AdvanceLocalProgress();
        progressTimer.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        refreshTimer?.Stop();
        progressTimer?.Stop();
        // WPF is tearing down its dispatcher here; perform async host disposal off the UI context.
        Task.Run(DisposeResourcesAsync).GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private async Task DisposeResourcesAsync()
    {
        refreshTimer?.Stop(); refreshTimer = null;
        progressTimer?.Stop(); progressTimer = null;
        var server = embeddedServer; embeddedServer = null;
        if (server is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await server.StopAsync(timeout.Token); } catch (OperationCanceledException) { }
            await server.DisposeAsync();
        }
        var provider = services; services = null;
        if (provider is not null) await provider.DisposeAsync();
    }

    private void QueueList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListView list || list.SelectedItem is not QueueEntry item) return;
        DragDrop.DoDragDrop(list, item, DragDropEffects.Move);
    }

    private async void QueueList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not ListView list || list.DataContext is not QueueManagementViewModel viewModel || e.Data.GetData(typeof(QueueEntry)) is not QueueEntry moving) return;
        var element = list.InputHitTest(e.GetPosition(list)) as DependencyObject;
        var container = ItemsControl.ContainerFromElement(list, element) as ListViewItem;
        await viewModel.MoveBeforeAsync(moving, container?.DataContext as QueueEntry);
    }
}
