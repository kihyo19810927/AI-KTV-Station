namespace Station.Application.Metadata;

public interface IMediaFilenameParser
{
    ParsedSongMetadata Parse(string relativePath);
}

public sealed record ParsedSongMetadata(
    string OriginalStem,
    string Title,
    string? ArtistDisplay,
    IReadOnlyList<string> ArtistCandidates,
    string? Quality,
    string? Language,
    string? Category,
    string? Version,
    double Confidence,
    IReadOnlyList<string> Warnings);
