using System.Runtime.CompilerServices;
using Station.Application.Scanning;
using Station.Domain.Models;

namespace Station.Infrastructure.Scanning;

public sealed class FileSystemMediaFileEnumerator : IMediaFileEnumerator
{
    public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pending = new Stack<string>();
        pending.Push(source.RootPath);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            string[] directories;
            string[] files;
            MediaEnumerationEntry? directoryError = null;
            try
            {
                directories = Directory.GetDirectories(directory);
                files = Directory.GetFiles(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                directories = [];
                files = [];
                directoryError = MediaEnumerationEntry.Error(ToRelative(source.RootPath, directory), "scan.directory_unreadable");
            }
            if (directoryError is not null) { yield return directoryError; continue; }
            foreach (var child in directories.OrderDescending()) pending.Push(child);
            foreach (var path in files.Order())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(Path.GetExtension(path), ".mkv", StringComparison.OrdinalIgnoreCase)) continue;
                MediaEnumerationEntry entry;
                try
                {
                    var info = new FileInfo(path);
                    entry = MediaEnumerationEntry.File(ToRelative(source.RootPath, path), info.Length, info.LastWriteTimeUtc);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException)
                {
                    entry = MediaEnumerationEntry.Error(ToRelative(source.RootPath, path), "scan.file_unreadable");
                }
                yield return entry;
                await Task.Yield();
            }
        }
    }

    private static string ToRelative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
