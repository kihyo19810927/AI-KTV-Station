using Station.Infrastructure.Media;

namespace Station.Core.Tests;

public sealed class FfprobeProcessIntegrationTests
{
    [Fact]
    public async Task Missing_executable_is_classified()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-ffprobe-{Guid.NewGuid():N}.exe");
        var result = await new FfprobeMediaProbe(missing, TimeSpan.FromSeconds(1)).ProbeAsync("unused.mkv");
        Assert.False(result.IsSuccess);
        Assert.Equal("media_probe.executable_missing", result.Error.Code);
    }

    [Fact]
    [Trait("Category", "External")]
    public async Task Probes_generated_unicode_mkv()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_FFPROBE");
        var media = Environment.GetEnvironmentVariable("KTV_STATION_MEDIA_FIXTURE");
        Assert.False(string.IsNullOrWhiteSpace(executable)); Assert.False(string.IsNullOrWhiteSpace(media));
        var result = await new FfprobeMediaProbe(executable!, TimeSpan.FromSeconds(10)).ProbeAsync(media!);
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.InRange(result.Value.DurationSeconds, 9.5, 10.5);
        Assert.Equal(4, result.Value.Tracks.Count);
    }
}
