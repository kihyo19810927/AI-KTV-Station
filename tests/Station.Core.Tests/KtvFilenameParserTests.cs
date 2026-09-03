using Station.Application.Metadata;

namespace Station.Core.Tests;

public sealed class KtvFilenameParserTests
{
    private readonly KtvFilenameParser parser = new();

    [Theory]
    [InlineData("马健涛-分手[1080P]-国语-流行.mkv", "分手", "马健涛", "1080P", "国语", "流行", null)]
    [InlineData("谢娜_A LIN黄丽玲_贾静雯-WHY OH WHY(繁体版)-国语-合唱.mkv", "WHY OH WHY", "谢娜_A LIN黄丽玲_贾静雯", null, "国语", "合唱", "繁体版")]
    [InlineData("周杰伦-夜曲(MTV)-国语-流行歌曲.mkv", "夜曲", "周杰伦", null, "国语", "流行歌曲", "MTV")]
    [InlineData("歌手－LOVE - SONG－国语－流行.mkv", "LOVE-SONG", "歌手", null, "国语", "流行", null)]
    public void Parses_structured_names(string file, string title, string artist, string? quality, string language, string category, string? version)
    {
        var result = parser.Parse(file);
        Assert.Equal(title, result.Title); Assert.Equal(artist, result.ArtistDisplay);
        Assert.Equal(quality, result.Quality); Assert.Equal(language, result.Language); Assert.Equal(category, result.Category); Assert.Equal(version, result.Version);
        Assert.True(result.Confidence >= 0.9); Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Preserves_multi_artist_display_and_produces_candidates()
    {
        var result = parser.Parse("甲_乙_丙-合唱曲-国语-合唱.mkv");
        Assert.Equal("甲_乙_丙", result.ArtistDisplay);
        Assert.Equal(["甲", "乙", "丙"], result.ArtistCandidates);
    }

    [Fact]
    public void Falls_back_to_full_stem_when_artist_is_missing()
    {
        var result = parser.Parse("无法解析的歌曲名称.mkv");
        Assert.Equal("无法解析的歌曲名称", result.Title);
        Assert.Contains("metadata.artist_missing", result.Warnings);
        Assert.Equal(0.4, result.Confidence);
    }
}
