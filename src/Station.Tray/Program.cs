using Microsoft.Extensions.DependencyInjection;
using Station.Application.Configuration;
using Station.Application.Playback;
using Station.Server.Hosting;

namespace Station.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var application = StationServerHost.Build([], webRootPath: Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        var runtime = new WebApplicationTrayRuntime(application);
        var options = application.Services.GetRequiredService<StationOptions>();
        var host = options.Server.BindAddress is "0.0.0.0" or "::" ? "127.0.0.1" : options.Server.BindAddress;
        var desktopUri = new Uri($"http://{host}:{options.Server.Port}/desk");
        var controller = new TrayStationController(runtime, application.Services.GetRequiredService<IPlayerAdapter>(), new EdgeAppLauncher(), desktopUri);
        System.Windows.Forms.Application.Run(new TrayApplicationContext(controller));
    }
}
