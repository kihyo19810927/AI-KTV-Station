using Station.Application.Metadata;
using Station.Infrastructure.Metadata;

namespace Station.Core.Tests;

public sealed class NfoMetadataTests
{
    [Fact]
    public async Task Reads_unicode_kodi_style_nfo_without_modifying_it()
    {
        var directory = CreateDirectory();
        var media = Path.Combine(directory, "歌手-文件名.mkv");
        var nfo = Path.ChangeExtension(media, ".nfo");
        const string xml = """
            <movie>
              <title>月光测试曲</title>
              <artist><name>歌手甲</name></artist>
              <artist>歌手乙</artist>
              <language>国语</language>
              <genre>流行</genre>
              <year>2026</year>
              <quality>4K</quality>
              <edition>现场版</edition>
            </movie>
            """;
        await File.WriteAllTextAsync(nfo, xml);
        try
        {
            var before = await File.ReadAllTextAsync(nfo);
            var result = await new NfoXmlMetadataReader().ReadForMediaAsync(media);
            Assert.NotNull(result.Metadata);
            Assert.Equal("月光测试曲", result.Metadata.Title);
            Assert.Equal(["歌手甲", "歌手乙"], result.Metadata.Artists);
            Assert.Equal(2026, result.Metadata.Year);
            Assert.Equal("流行", result.Metadata.Category);
            Assert.Equal("现场版", result.Metadata.Version);
            Assert.Empty(result.Warnings);
            Assert.Equal(before, await File.ReadAllTextAsync(nfo));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Missing_nfo_is_optional_and_malformed_or_dtd_nfo_becomes_warning()
    {
        var directory = CreateDirectory();
        var media = Path.Combine(directory, "song.mkv");
        try
        {
            var reader = new NfoXmlMetadataReader();
            var missing = await reader.ReadForMediaAsync(media);
            Assert.Null(missing.Metadata);
            Assert.Empty(missing.Warnings);

            await File.WriteAllTextAsync(Path.ChangeExtension(media, ".nfo"), "<song><title>broken");
            Assert.Contains("metadata.nfo_unreadable", (await reader.ReadForMediaAsync(media)).Warnings);

            await File.WriteAllTextAsync(Path.ChangeExtension(media, ".nfo"), "<!DOCTYPE song [<!ENTITY xxe SYSTEM 'file:///C:/Windows/win.ini'>]><song><title>&xxe;</title></song>");
            var prohibited = await reader.ReadForMediaAsync(media);
            Assert.Null(prohibited.Metadata);
            Assert.Contains("metadata.nfo_unreadable", prohibited.Warnings);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Resolution_priority_is_manual_then_nfo_then_filename_and_reports_mismatch()
    {
        var filename = new ParsedSongMetadata("歌手甲-文件标题", "文件标题", "歌手甲", ["歌手甲"], "1080P", "国语", "流行", null, 0.95, []);
        var nfo = new NfoReadResult(new NfoSongMetadata("NFO 标题", ["歌手乙"], "粤语", "经典", 2020, "4K", "现场版"), []);
        var manual = new ManualSongMetadata(Title: "人工标题", Language: "日语", Quality: "8K");

        var result = SongMetadataResolver.Resolve(filename, nfo, manual);

        Assert.Equal("人工标题", result.Title);
        Assert.Equal(["歌手乙"], result.Artists);
        Assert.Equal("日语", result.Language);
        Assert.Equal("经典", result.Category);
        Assert.Equal(2020, result.Year);
        Assert.Equal("8K", result.Quality);
        Assert.Equal("现场版", result.Version);
        Assert.Contains("metadata.nfo_title_mismatch", result.Warnings);
        Assert.Contains("metadata.nfo_artist_mismatch", result.Warnings);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ai-ktv-nfo-{Guid.NewGuid():N}", "Unicode 测试");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
