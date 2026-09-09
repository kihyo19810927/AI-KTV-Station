using Microsoft.Extensions.DependencyInjection;
using Station.Desktop.ViewModels;

namespace Station.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var collection = new ServiceCollection();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
