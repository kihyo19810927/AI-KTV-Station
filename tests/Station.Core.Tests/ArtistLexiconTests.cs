using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class ArtistLexiconTests
{
    [Fact]
    public void Embedded_artist_images_use_https_for_browser_compatibility()
    {
        var lexicon = new ArtistLexicon();
        var known = new[] { "周杰伦", "张学友", "薛之谦" }
            .Select(name => lexicon.Resolve(name))
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.ImageUrl));

        Assert.NotNull(known);
        Assert.StartsWith("https://", known!.ImageUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Version_two_lexicon_keeps_same_name_artists_and_uses_language_to_disambiguate()
    {
        const string json = """
            {"schemaVersion":2,"artists":[
              {"id":"kr-lisa","name":"Lisa","aliases":["리사"],"country":"韩国","group":"韩国歌手","popularity":100,"imageUrl":"https://example.test/kr.jpg","source":"test","sourceArtistId":"1"},
              {"id":"jp-lisa","name":"Lisa","aliases":["LiSA"],"country":"日本","group":"日本歌手","popularity":99,"imageUrl":"https://example.test/jp.jpg","source":"test","sourceArtistId":"2"}
            ]}
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var lexicon = new ArtistLexicon(ArtistLexicon.Load(stream));

        Assert.Equal("kr-lisa", lexicon.Resolve("Lisa", "韩语").Id);
        Assert.Equal("jp-lisa", lexicon.Resolve("Lisa", "日语").Id);
        Assert.Equal("jp-lisa", lexicon.Resolve("LiSA", "日语").Id);
    }

    [Fact]
    public void Legacy_lexicon_format_remains_readable()
    {
        const string json = """{"Lyn":{"group":"韩国歌手","popularity":99,"imageUrl":"http://example.test/lyn.jpg"}}""";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var lexicon = new ArtistLexicon(ArtistLexicon.Load(stream));

        var lyn = lexicon.Resolve("Lyn", "韩语");
        Assert.Equal("韩国", lyn.Country);
        Assert.Equal("韩国歌手", lyn.Group);
        Assert.Equal("https://example.test/lyn.jpg", lyn.ImageUrl);
    }
}
