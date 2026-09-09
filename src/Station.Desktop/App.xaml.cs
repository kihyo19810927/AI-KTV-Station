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
using Station.Infrastructure.Metadata;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;

namespace Station.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var collection = new ServiceCollection();
        var options = new StationOptions();
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, options.Storage.DataDirectory);
        collection.AddSingleton(options);
        collection.AddSingleton(TimeProvider.System);
        var databaseOptions = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={Path.Combine(dataDirectory, "station.db")}").Options;
        collection.AddSingleton(new StationDbContext(databaseOptions));
        collection.AddSingleton<IStationHealthService, StationHealthService>();
        collection.AddSingleton<IPlayerAdapter>(_ => new MpvPlayerAdapter(new PlayerOptions
        {
            ExecutablePath = FindMpvExecutable() ?? "mpv.exe",
            CommandTimeoutSeconds = options.Player.CommandTimeoutSeconds,
        }));
        collection.AddSingleton<PlaybackControlService>();
        collection.AddSingleton<PlaybackConsoleViewModel>();
        collection.AddSingleton<IRoomQueueRepository, EfRoomQueueRepository>();
        collection.AddSingleton<IRoomQueueLock, InProcessRoomQueueLock>();
        collection.AddSingleton<IRoomQueueService, RoomQueueService>();
        collection.AddSingleton<HostRoomContext>();
        collection.AddSingleton<QueueManagementViewModel>();
        collection.AddSingleton<IMediaSourceRepository, EfMediaSourceRepository>();
        collection.AddSingleton<IMediaPathInspector, FileSystemMediaPathInspector>();
        collection.AddSingleton<IMediaSourceService, MediaSourceService>();
        collection.AddSingleton<ISearchTextNormalizer, ToolGoodSearchTextNormalizer>();
        collection.AddSingleton<ISongSearchIndex, SqliteSongSearchIndex>();
        collection.AddSingleton<ICatalogAdminRepository, EfCatalogAdminRepository>();
        collection.AddSingleton<ICatalogAdminService, CatalogAdminService>();
        collection.AddSingleton<IMediaScanRepository, EfMediaScanRepository>();
        collection.AddSingleton<IMediaFileEnumerator, FileSystemMediaFileEnumerator>();
        collection.AddSingleton<IMediaFilenameParser, KtvFilenameParser>();
        collection.AddSingleton<INfoMetadataReader, NfoXmlMetadataReader>();
        collection.AddSingleton<IMediaProbe>(_ => new FfprobeMediaProbe(FindExecutable("ffprobe.exe") ?? "ffprobe.exe", TimeSpan.FromSeconds(30)));
        collection.AddSingleton<IMediaScanRunner, MediaScanService>();
        collection.AddSingleton<ICatalogScanService, CatalogScanService>();
        collection.AddSingleton<CatalogManagementViewModel>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await services.GetRequiredService<StationDbContext>().Database.MigrateAsync();
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        await services.GetRequiredService<MainWindowViewModel>().RefreshHealthAsync();
        await services.GetRequiredService<CatalogManagementViewModel>().InitializeAsync();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private static string? FindMpvExecutable()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, "mpv.exe");
            if (File.Exists(candidate)) return candidate;
        }
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        return Directory.Exists(root) ? Directory.EnumerateFiles(root, "mpv.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
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
