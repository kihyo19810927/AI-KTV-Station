using Station.Domain.Models;

namespace Station.Application.Scanning;

public interface IMediaFileEnumerator
{
    IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, CancellationToken cancellationToken = default);
}

public sealed record MediaEnumerationEntry(string RelativePath, long? SizeBytes, DateTimeOffset? LastWriteTime, string? ErrorCode)
{
    public bool IsError => ErrorCode is not null;
    public static MediaEnumerationEntry File(string relativePath, long sizeBytes, DateTimeOffset lastWriteTime) => new(relativePath, sizeBytes, lastWriteTime, null);
    public static MediaEnumerationEntry Error(string relativePath, string errorCode) => new(relativePath, null, null, errorCode);
}
