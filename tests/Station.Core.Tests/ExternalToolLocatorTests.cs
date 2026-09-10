using Station.Infrastructure.Runtime;

namespace Station.Core.Tests;

public sealed class ExternalToolLocatorTests
{
    [Fact]
    public void Common_directory_has_priority_over_path()
    {
        var root = Path.Combine(Path.GetTempPath(), $"station-tools-{Guid.NewGuid():N}");
        var app = Path.Combine(root, "src", "bin");
        var common = Path.Combine(root, "common", "mpv");
        var path = Path.Combine(root, "path");
        try
        {
            Directory.CreateDirectory(app); Directory.CreateDirectory(common); Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(common, "mpv.exe"), string.Empty);
            File.WriteAllText(Path.Combine(path, "mpv.exe"), string.Empty);

            Assert.Equal(Path.Combine(common, "mpv.exe"), ExternalToolLocator.Find("mpv.exe", app, path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Ffprobe_uses_ffmpeg_common_subdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"station-tools-{Guid.NewGuid():N}");
        var common = Path.Combine(root, "common", "ffmpeg");
        try
        {
            Directory.CreateDirectory(common);
            var expected = Path.Combine(common, "ffprobe.exe");
            File.WriteAllText(expected, string.Empty);

            Assert.Equal(expected, ExternalToolLocator.Find("ffprobe.exe", root, string.Empty));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
