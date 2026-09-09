using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IMediaFileEnumerator
{
    IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, CancellationToken cancellationToken = default);
}

public enum MediaEntryKind { PlayableMedia, LyricsSidecar, IgnoredArchive }

public static class MediaFormatPolicy
{
    private static readonly HashSet<string> PlayableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mkv", ".mpg", ".mpeg", ".mp4", ".avi", ".vob", ".ts", ".m2ts", ".wmv", ".mov" };

    public static MediaEntryKind? Classify(string path)
    {
        var extension = Path.GetExtension(path);
        if (PlayableExtensions.Contains(extension)) return MediaEntryKind.PlayableMedia;
        if (extension.Equals(".ksc", StringComparison.OrdinalIgnoreCase)) return MediaEntryKind.LyricsSidecar;
        if (extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)) return MediaEntryKind.IgnoredArchive;
        return null;
    }
}

public sealed record MediaEnumerationEntry(string RelativePath, long? SizeBytes, DateTimeOffset? LastWriteTime, string? ErrorCode, MediaEntryKind Kind = MediaEntryKind.PlayableMedia)
{
    public bool IsError => ErrorCode is not null;
    public static MediaEnumerationEntry File(string relativePath, long sizeBytes, DateTimeOffset lastWriteTime, MediaEntryKind kind = MediaEntryKind.PlayableMedia) => new(relativePath, sizeBytes, lastWriteTime, null, kind);
    public static MediaEnumerationEntry Error(string relativePath, string errorCode) => new(relativePath, null, null, errorCode);
}
