using System.Reflection;
using System.Text.Json;

namespace Station.Infrastructure.Search;

public sealed record ArtistLexiconEntry(string Id, string Name, IReadOnlyList<string> Aliases, string Country,
    string Group, int Popularity, string? ImageUrl, string Source, string? SourceArtistId);

public sealed class ArtistLexicon
{
    private readonly IReadOnlyList<ArtistLexiconEntry> entries;

    public ArtistLexicon() : this(LoadEmbedded()) { }

    internal ArtistLexicon(IReadOnlyList<ArtistLexiconEntry> entries) => this.entries = entries;

    public ArtistLexiconEntry Resolve(string name, string? language = null)
    {
        var trimmed = name.Trim();
        var candidates = entries.Where(entry => string.Equals(entry.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
            entry.Aliases.Any(alias => string.Equals(alias, trimmed, StringComparison.OrdinalIgnoreCase))).ToArray();
        var preferredGroup = language?.Trim() switch { "韩语" => "韩国歌手", "日语" => "日本歌手", _ => null };
        var entry = candidates
            .OrderByDescending(candidate => preferredGroup is not null && string.Equals(candidate.Group, preferredGroup, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => string.Equals(candidate.Name, trimmed, StringComparison.Ordinal))
            .ThenByDescending(candidate => candidate.Popularity)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (entry is null) return new("unknown", trimmed, [], "未知", "其他", 0, null, "none", null);
        var imageUrl = entry.ImageUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? "https://" + entry.ImageUrl[7..]
            : entry.ImageUrl;
        return entry with { ImageUrl = imageUrl };
    }

    private static IReadOnlyList<ArtistLexiconEntry> LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Station.Infrastructure.Search.artists.json")
            ?? throw new InvalidOperationException("Embedded artist lexicon is missing.");
        return Load(stream);
    }

    internal static IReadOnlyList<ArtistLexiconEntry> Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("artists", out var artists))
            return JsonSerializer.Deserialize<List<ArtistLexiconEntry>>(artists.GetRawText(), options) ?? [];
        var legacy = JsonSerializer.Deserialize<Dictionary<string, LegacyArtistLexiconEntry>>(document.RootElement.GetRawText(), options) ?? [];
        return legacy.Select(pair => new ArtistLexiconEntry($"legacy:{pair.Key}", pair.Key, [], CountryFromGroup(pair.Value.Group),
            pair.Value.Group, pair.Value.Popularity, pair.Value.ImageUrl, "legacy", null)).ToArray();
    }

    private static string CountryFromGroup(string group) => group switch
    {
        "日本歌手" => "日本",
        "韩国歌手" => "韩国",
        "华语男歌手" or "华语女歌手" or "华语组合" => "华语地区",
        "欧美歌手" => "欧美地区",
        _ => "未知",
    };

    private sealed record LegacyArtistLexiconEntry(string Group, int Popularity, string? ImageUrl);
}
