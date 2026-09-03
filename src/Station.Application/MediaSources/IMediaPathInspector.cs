namespace Station.Application.MediaSources;

public interface IMediaPathInspector
{
    ValueTask<MediaPathCheck> InspectAsync(string path, CancellationToken cancellationToken = default);
}

public sealed record MediaPathCheck(string CanonicalPath, bool Exists, bool IsDirectory, bool IsReadable, string? ErrorCode = null);
