using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Station.Desktop.ViewModels;
using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Infrastructure.Health;
using Station.Infrastructure.Persistence;

namespace Station.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
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
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        _ = services.GetRequiredService<MainWindowViewModel>().RefreshHealthAsync();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
