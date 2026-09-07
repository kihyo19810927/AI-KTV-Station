namespace Station.Application.Metadata;

public sealed record ManualSongMetadata(
    string? Title = null,
    IReadOnlyList<string>? Artists = null,
    string? Language = null,
    string? Category = null,
    int? Year = null,
    string? Quality = null,
    string? Version = null);

public sealed record ResolvedSongMetadata(
    string Title,
    IReadOnlyList<string> Artists,
    string? Language,
    string? Category,
    int? Year,
    string? Quality,
    string? Version,
    IReadOnlyList<string> Warnings);

public static class SongMetadataResolver
{
    public static ResolvedSongMetadata Resolve(
        ParsedSongMetadata filename,
        NfoReadResult? nfoRead = null,
        ManualSongMetadata? manual = null)
    {
        var nfo = nfoRead?.Metadata;
        var warnings = new List<string>(filename.Warnings);
        if (nfoRead is not null) warnings.AddRange(nfoRead.Warnings);
        AddMismatchWarnings(filename, nfo, warnings);

        return new ResolvedSongMetadata(
            First(manual?.Title, nfo?.Title, filename.Title)!,
            FirstArtists(manual?.Artists, nfo?.Artists, filename.ArtistCandidates),
            First(manual?.Language, nfo?.Language, filename.Language),
            First(manual?.Category, nfo?.Category, filename.Category),
            manual?.Year ?? nfo?.Year,
            First(manual?.Quality, nfo?.Quality, filename.Quality),
            First(manual?.Version, nfo?.Version, filename.Version),
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void AddMismatchWarnings(ParsedSongMetadata filename, NfoSongMetadata? nfo, ICollection<string> warnings)
    {
        if (nfo is null) return;
        if (HasValue(nfo.Title) && !Equivalent(nfo.Title, filename.Title)) warnings.Add("metadata.nfo_title_mismatch");
        if (nfo.Artists.Count > 0 && filename.ArtistCandidates.Count > 0 &&
            !nfo.Artists.SequenceEqual(filename.ArtistCandidates, StringComparer.OrdinalIgnoreCase))
            warnings.Add("metadata.nfo_artist_mismatch");
    }

    private static IReadOnlyList<string> FirstArtists(params IReadOnlyList<string>?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var values = candidate?.Where(HasValue).Select(x => x.Trim()).ToArray() ?? [];
            if (values.Length > 0) return values;
        }
        return [];
    }

    private static string? First(params string?[] candidates) => candidates.FirstOrDefault(HasValue)?.Trim();
    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Equivalent(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
