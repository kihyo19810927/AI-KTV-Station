using Station.Application.MediaSources;

namespace Station.Infrastructure.MediaSources;

public sealed class FileSystemMediaPathInspector : IMediaPathInspector
{
    public ValueTask<MediaPathCheck> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return ValueTask.FromResult(new MediaPathCheck(string.Empty, false, false, false, "media_source.path_invalid"));
            var canonical = Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(canonical)) return ValueTask.FromResult(new MediaPathCheck(canonical, false, false, false, "media_source.path_missing"));
            using var enumerator = Directory.EnumerateFileSystemEntries(canonical).GetEnumerator();
            _ = enumerator.MoveNext();
            return ValueTask.FromResult(new MediaPathCheck(canonical, true, true, true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ValueTask.FromResult(new MediaPathCheck(path, false, false, false, "media_source.path_unreadable"));
        }
    }
}
