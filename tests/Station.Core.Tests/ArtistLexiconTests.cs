using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class ArtistLexiconTests
{
    [Fact]
    public void Embedded_artist_images_use_https_for_browser_compatibility()
    {
        var lexicon = new ArtistLexicon();
        var known = new[] { "周杰伦", "张学友", "薛之谦" }
            .Select(lexicon.Resolve)
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.ImageUrl));

        Assert.NotNull(known);
        Assert.StartsWith("https://", known!.ImageUrl, StringComparison.OrdinalIgnoreCase);
    }
}
