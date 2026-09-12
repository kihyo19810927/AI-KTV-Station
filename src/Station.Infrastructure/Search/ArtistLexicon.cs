using System.Reflection;
using System.Text.Json;

namespace Station.Infrastructure.Search;

public sealed record ArtistLexiconEntry(string Group, int Popularity, string? ImageUrl);

public sealed class ArtistLexicon
{
    private readonly IReadOnlyDictionary<string, ArtistLexiconEntry> entries;

    public ArtistLexicon() : this(LoadEmbedded()) { }

    internal ArtistLexicon(IReadOnlyDictionary<string, ArtistLexiconEntry> entries) => this.entries = entries;

    public ArtistLexiconEntry Resolve(string name) =>
        entries.TryGetValue(name.Trim(), out var entry) ? entry : new("其他", 0, null);

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
