namespace Station.Server.Hosting;

/// <summary>
/// Keeps host-owned files together without exposing their absolute paths from public APIs.
/// </summary>
public sealed class StationRuntimePaths
{
    private StationRuntimePaths(string settingsRoot)
    {
        SettingsRoot = settingsRoot;
        SettingsFile = Path.Combine(settingsRoot, "settings.json");
        LogFile = Path.Combine(settingsRoot, "logs", "station.jsonl");
        DiagnosticsDirectory = Path.Combine(settingsRoot, "diagnostics");
    }

    public string SettingsRoot { get; }
    public string SettingsFile { get; }
    public string LogFile { get; }
    public string DiagnosticsDirectory { get; }

    public static StationRuntimePaths Create()
    {
        var root = Environment.GetEnvironmentVariable("AI_KTV_STATION_SETTINGS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-KTV Station");
        return new(Path.GetFullPath(root));
    }
}
