using System.Reflection;
using System.Text.Json;

namespace Station.Infrastructure.Search;

public sealed record ArtistLexiconEntry(string Group, int Popularity, string? ImageUrl);

public sealed class ArtistLexicon
{
    private readonly IReadOnlyDictionary<string, ArtistLexiconEntry> entries;

    public ArtistLexicon() : this(LoadEmbedded()) { }

    internal ArtistLexicon(IReadOnlyDictionary<string, ArtistLexiconEntry> entries) => this.entries = entries;

    public ArtistLexiconEntry Resolve(string name)
    {
        if (!entries.TryGetValue(name.Trim(), out var entry)) return new("其他", 0, null);
        var imageUrl = entry.ImageUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? "https://" + entry.ImageUrl[7..]
            : entry.ImageUrl;
        return entry with { ImageUrl = imageUrl };
    }

    private static IReadOnlyDictionary<string, ArtistLexiconEntry> LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Station.Infrastructure.Search.artists.json")
            ?? throw new InvalidOperationException("Embedded artist lexicon is missing.");
        var loaded = JsonSerializer.Deserialize<Dictionary<string, ArtistLexiconEntry>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];
        return new Dictionary<string, ArtistLexiconEntry>(loaded, StringComparer.OrdinalIgnoreCase);
    }
}
