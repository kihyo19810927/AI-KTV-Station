namespace Station.Infrastructure.Runtime;

public static class ExternalToolLocator
{
    public static string? Find(string executableName, string? baseDirectory = null, string? pathEnvironment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        foreach (var common in EnumerateCommonCandidates(executableName, baseDirectory ?? AppContext.BaseDirectory))
        {
            if (File.Exists(common)) return common;
        }

        foreach (var directory in (pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;
        }

        return executableName.Equals("mpv.exe", StringComparison.OrdinalIgnoreCase) ? FindWingetMpv() : null;
    }

    private static IEnumerable<string> EnumerateCommonCandidates(string executableName, string baseDirectory)
    {
        var toolFolder = executableName.StartsWith("ff", StringComparison.OrdinalIgnoreCase) ? "ffmpeg" : "mpv";
        var current = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        while (current is not null)
        {
            yield return Path.Combine(current.FullName, "common", toolFolder, executableName);
            current = current.Parent;
        }
    }

    private static string? FindWingetMpv()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "mpv.exe", SearchOption.AllDirectories).FirstOrDefault()
            : null;
    }
}
