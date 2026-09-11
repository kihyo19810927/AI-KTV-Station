using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Catalog;

public sealed class CatalogJsonImportService(
    StationDbContext database,
    ISearchTextNormalizer normalizer,
    ISongSearchIndex searchIndex) : ICatalogJsonImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<CatalogImportResult>> ImportAsync(string indexPath, string mountRoot,
        IProgress<CatalogImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(indexPath)) return Failure("catalog.import_file_missing", "JSON/JSONL file is unavailable.");
        if (string.IsNullOrWhiteSpace(mountRoot)) return Failure("catalog.import_root_required", "Mounted media root is required.");
        string root;
        try { root = Path.GetFullPath(mountRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure("catalog.import_root_invalid", "Mounted media root is invalid.");
        }
        var source = await database.MediaSources.FirstOrDefaultAsync(x => x.RootPath == root, cancellationToken);
        if (source is null)
        {
            source = new MediaSource { Name = "115 JSON 曲库", RootPath = root, Availability = Directory.Exists(root) ? AvailabilityStatus.Available : AvailabilityStatus.Offline };
            database.MediaSources.Add(source); await database.SaveChangesAsync(cancellationToken);
        }
        var sourceId = source.Id;
        var existing = (await database.MediaFiles.Where(x => x.MediaSourceId == source.Id)
            .Select(x => x.RelativePath).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        long read = 0, added = 0, skipped = 0, errors = 0;
        await foreach (var record in ReadAsync(indexPath, cancellationToken))
        {
            read++;
            var relative = (record.RelativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0 || existing.Contains(relative)) { skipped++; continue; }
            try
            {
                var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { errors++; continue; }
                var title = string.IsNullOrWhiteSpace(record.Title) ? Path.GetFileNameWithoutExtension(relative) : record.Title.Trim();
                var titleKeys = normalizer.CreateKeys(title);
                var song = new Song { Title = title, NormalizedTitle = titleKeys.Normalized, SimplifiedTitle = titleKeys.Simplified, TraditionalTitle = titleKeys.Traditional, TitlePinyin = titleKeys.Pinyin, TitleInitials = titleKeys.Initials, CompactTitle = titleKeys.Compact, Language = record.Language, Category = record.Category, ArtistGroup = "其他", Year = InferYear(relative), Availability = AvailabilityStatus.Available };
                var artists = record.Artists is { Count: > 0 } ? record.Artists : string.IsNullOrWhiteSpace(record.Artist) ? ["未知歌手"] : [record.Artist.Trim()];
                for (var order = 0; order < artists.Count; order++)
                {
                    var name = artists[order].Trim(); var keys = normalizer.CreateKeys(name);
                    song.Artists.Add(new SongArtist { Order = order, Artist = new Artist { Name = name, NormalizedName = keys.Normalized, SimplifiedName = keys.Simplified, TraditionalName = keys.Traditional, Pinyin = keys.Pinyin, Initials = keys.Initials, CompactName = keys.Compact } });
                }
                database.MediaFiles.Add(new MediaFile { Song = song, MediaSourceId = sourceId, RelativePath = relative, SizeBytes = record.SizeBytes ?? 0, LastWriteTime = DateTimeOffset.UnixEpoch, DurationSeconds = record.DurationMs is null ? null : record.DurationMs / 1000d, Availability = AvailabilityStatus.Available });
                existing.Add(relative); added++;
                if (added % 500 == 0) { await database.SaveChangesAsync(cancellationToken); database.ChangeTracker.Clear(); progress?.Report(new(read, added, skipped, errors)); }
            }
            catch (Exception) { errors++; }
        }
        await database.SaveChangesAsync(cancellationToken);
        await searchIndex.RebuildAsync(cancellationToken);
        progress?.Report(new(read, added, skipped, errors));
        return Result<CatalogImportResult>.Success(new(read, added, skipped, errors));
    }

    private static async IAsyncEnumerable<ImportRecord> ReadAsync(string path, [EnumeratorCancellation] CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        if (Path.GetExtension(path).Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(token) is { } line)
                if (!string.IsNullOrWhiteSpace(line) && JsonSerializer.Deserialize<ImportRecord>(line, JsonOptions) is { } item) yield return item;
            yield break;
        }
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<ImportRecord>(stream, JsonOptions, token))
            if (item is not null) yield return item;
    }

    private static int? InferYear(string path) { var valueText = path.Split('/')[0].TrimEnd('年'); return int.TryParse(valueText, out var value) ? value < 100 ? value <= 30 ? 2000 + value : 1900 + value : value : null; }
    private static Result<CatalogImportResult> Failure(string code, string message) => Result<CatalogImportResult>.Failure(new Error(code, message));
    private sealed record ImportRecord(string? RelativePath, string? Artist, List<string>? Artists, string? Title, string? Language, string? Category, long? SizeBytes, double? DurationMs);
}
