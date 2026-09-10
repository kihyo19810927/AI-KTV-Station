using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Station.CatalogImport <index.json> <mount-root> <station.db>");
    return 2;
}
var jsonPath = Path.GetFullPath(args[0]); var mountRoot = Path.GetFullPath(args[1]); var databasePath = Path.GetFullPath(args[2]);
if (!File.Exists(jsonPath) || !Directory.Exists(mountRoot)) { Console.Error.WriteLine("JSON file or mount root is unavailable."); return 3; }
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={databasePath}").Options;
await using var database = new StationDbContext(options);
await new DatabaseUpgradeService(database, new EfDatabaseMigrationExecutor(), TimeProvider.System).UpgradeAsync();
var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups"); Directory.CreateDirectory(backupDirectory);
var backupPath = Path.Combine(backupDirectory, $"station-before-json-import-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.db");
await using (var source = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly"))
await using (var destination = new SqliteConnection($"Data Source={backupPath};Mode=ReadWriteCreate"))
{ await source.OpenAsync(); await destination.OpenAsync(); source.BackupDatabase(destination); }

await using var stream = File.OpenRead(jsonPath);
var records = await JsonSerializer.DeserializeAsync<List<ImportSong>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
var rootKey = mountRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var mediaSource = await database.MediaSources.FirstOrDefaultAsync(x => x.RootPath == rootKey);
if (mediaSource is null) { mediaSource = new MediaSource { Name = "115 曲库", RootPath = rootKey, Availability = AvailabilityStatus.Available }; database.MediaSources.Add(mediaSource); await database.SaveChangesAsync(); }
var existingPaths = (await database.MediaFiles.Where(x => x.MediaSourceId == mediaSource.Id).Select(x => x.RelativePath).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
var normalizer = new ToolGoodSearchTextNormalizer(); var imported = 0; var skipped = 0;
foreach (var record in records)
{
    var relative = (record.RelativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
    if (relative.Length == 0 || existingPaths.Contains(relative)) { skipped++; continue; }
    var combined = Path.GetFullPath(Path.Combine(rootKey, relative.Replace('/', Path.DirectorySeparatorChar)));
    if (!combined.StartsWith(rootKey + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
    var title = string.IsNullOrWhiteSpace(record.Title) ? Path.GetFileNameWithoutExtension(relative) : record.Title.Trim();
    var titleKeys = normalizer.CreateKeys(title);
    var song = new Song { Title = title, NormalizedTitle = titleKeys.Normalized, SimplifiedTitle = titleKeys.Simplified, TraditionalTitle = titleKeys.Traditional, TitlePinyin = titleKeys.Pinyin, TitleInitials = titleKeys.Initials, CompactTitle = titleKeys.Compact, Language = record.Language, Category = record.Category, ArtistGroup = "其他", Year = InferYear(relative), Availability = AvailabilityStatus.Available };
    var names = record.Artists is { Count: > 0 } ? record.Artists : ["未知歌手"];
    for (var order = 0; order < names.Count; order++) { var name = names[order].Trim(); var keys = normalizer.CreateKeys(name); song.Artists.Add(new SongArtist { Order = order, Artist = new Artist { Name = name, NormalizedName = keys.Normalized, SimplifiedName = keys.Simplified, TraditionalName = keys.Traditional, Pinyin = keys.Pinyin, Initials = keys.Initials, CompactName = keys.Compact } }); }
    var media = new MediaFile { Song = song, MediaSourceId = mediaSource.Id, RelativePath = relative, SizeBytes = record.SizeBytes ?? 0, LastWriteTime = DateTimeOffset.UnixEpoch, DurationSeconds = record.Duration, Availability = AvailabilityStatus.Available };
    if (record.BackingTrackId is not null || record.VocalTrackId is not null) media.TrackMapping = new TrackMapping { BackingTrackId = record.BackingTrackId, VocalTrackId = record.VocalTrackId };
    database.MediaFiles.Add(media); existingPaths.Add(relative); imported++;
    if (imported % 500 == 0) { await database.SaveChangesAsync(); Console.WriteLine($"IMPORTED={imported};SKIPPED={skipped}"); }
}
await database.SaveChangesAsync();
await new SqliteSongSearchIndex(database, normalizer).RebuildAsync();
Console.WriteLine($"IMPORT_COMPLETE={imported};SKIPPED={skipped};TOTAL={records.Count};BACKUP={backupPath}");
return 0;

static int? InferYear(string path)
{
    var segment = path.Split('/')[0].TrimEnd('年');
    if (!int.TryParse(segment, out var value)) return null;
    return value < 100 ? (value <= 30 ? 2000 + value : 1900 + value) : value;
}

sealed record ImportSong(
    [property: JsonPropertyName("relative_path")] string? RelativePath,
    [property: JsonPropertyName("artists")] List<string>? Artists,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("size_bytes")] long? SizeBytes,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("backing_track_id")] int? BackingTrackId,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("vocal_track_id")] int? VocalTrackId);
