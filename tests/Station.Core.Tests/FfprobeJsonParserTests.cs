using Station.Domain.Models;
using Station.Infrastructure.Media;

namespace Station.Core.Tests;

public sealed class FfprobeJsonParserTests
{
    [Fact]
    public void Parses_duration_tracks_and_unicode_titles()
    {
        const string json = """
        {"streams":[
          {"index":0,"codec_name":"h264","codec_type":"video"},
          {"index":1,"codec_name":"aac","codec_type":"audio","tags":{"title":"伴奏"}},
          {"index":2,"codec_name":"aac","codec_type":"audio","tags":{"title":"原唱"}},
          {"index":3,"codec_name":"subrip","codec_type":"subtitle","tags":{"language":"zho","title":"测试字幕"}}
        ],"format":{"duration":"10.023000"}}
        """;
        var result = FfprobeJsonParser.Parse(json);
        Assert.True(result.IsSuccess); Assert.Equal(10.023, result.Value.DurationSeconds, 3);
        Assert.Equal(4, result.Value.Tracks.Count); Assert.Equal(2, result.Value.Tracks.Count(x => x.Type == MediaTrackType.Audio));
        Assert.Contains(result.Value.Tracks, x => x.Title == "伴奏"); Assert.Contains(result.Value.Tracks, x => x.Title == "原唱");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    public void Invalid_payload_is_classified(string json) => Assert.StartsWith("media_probe.invalid", FfprobeJsonParser.Parse(json).Error.Code);
}
