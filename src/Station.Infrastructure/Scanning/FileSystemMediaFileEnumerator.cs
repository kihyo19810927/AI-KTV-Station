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
            FileInfo[] files;
            MediaEnumerationEntry? directoryError = null;
            try
            {
                directories = Directory.GetDirectories(directory);
                files = new DirectoryInfo(directory).GetFiles();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                directories = [];
                files = [];
                directoryError = MediaEnumerationEntry.Error(ToRelative(source.RootPath, directory), "scan.directory_unreadable");
            }
            if (directoryError is not null) { yield return directoryError; continue; }
            foreach (var child in directories.OrderDescending()) pending.Push(child);
            foreach (var info in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var path = info.FullName;
                cancellationToken.ThrowIfCancellationRequested();
                var kind = MediaFormatPolicy.Classify(path);
                if (kind is null) continue;
                MediaEnumerationEntry entry;
                try
                {
                    entry = MediaEnumerationEntry.File(ToRelative(source.RootPath, path), info.Length, info.LastWriteTimeUtc, kind.Value);
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
