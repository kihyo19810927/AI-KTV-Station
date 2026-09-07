namespace Station.Application.Metadata;

public interface INfoMetadataReader
{
    Task<NfoReadResult> ReadForMediaAsync(string mediaPath, CancellationToken cancellationToken = default);
}

public sealed record NfoReadResult(NfoSongMetadata? Metadata, IReadOnlyList<string> Warnings)
{
    public static NfoReadResult Missing { get; } = new(null, []);
}

public sealed record NfoSongMetadata(
    string? Title,
    IReadOnlyList<string> Artists,
    string? Language,
    string? Category,
    int? Year,
    string? Quality,
    string? Version);
