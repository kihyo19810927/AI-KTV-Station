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
        var exitAfter = ParseSmokeExitAfter(Environment.GetCommandLineArgs());
        IEdgeAppLauncher edge = exitAfter is null ? new EdgeAppLauncher() : new SmokeEdgeAppLauncher();
        var controller = new TrayStationController(runtime, application.Services.GetRequiredService<IPlayerAdapter>(), edge, desktopUri);
        var context = new TrayApplicationContext(controller);
        if (exitAfter is not null) context.ScheduleSmokeExit(exitAfter.Value);
        System.Windows.Forms.Application.Run(context);
    }

    // CI/package smoke only. Normal users never receive this argument and the
    // process remains controlled by the notification-area Exit command.
    private static TimeSpan? ParseSmokeExitAfter(IEnumerable<string> arguments)
    {
        const string prefix = "--smoke-exit-after=";
        var value = arguments.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return value is not null && int.TryParse(value[prefix.Length..], out var seconds) && seconds is >= 3 and <= 60
            ? TimeSpan.FromSeconds(seconds)
            : null;
    }

    private sealed class SmokeEdgeAppLauncher : IEdgeAppLauncher
    {
        public bool TryOpen(Uri _) => true;
    }
}
