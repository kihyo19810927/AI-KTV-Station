using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Media;
using Station.Application.Metadata;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class MediaScanServiceTests
{
    [Fact]
    public async Task Mpg_is_indexed_without_nfo_ksc_is_linked_and_rar_is_not_playable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-formats-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "歌手甲-测试歌-国语-流行.mpg"), "video");
        await File.WriteAllTextAsync(Path.Combine(root, "歌手甲-测试歌-国语-流行.ksc"), "karaoke lyrics");
        await File.WriteAllTextAsync(Path.Combine(root, "待解压曲包.rar"), "archive");
        try
        {
            await using var database = CreateDatabase();
            await database.Database.EnsureCreatedAsync();
            var source = new MediaSource { Name = "Fixture", RootPath = root, Availability = AvailabilityStatus.Available };
            database.MediaSources.Add(source); await database.SaveChangesAsync();

            var result = await new MediaScanService(new EfMediaScanRepository(database), new FileSystemMediaFileEnumerator()).ScanAsync(source.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.DiscoveredFiles);
            var media = await database.MediaFiles.Include(x => x.Song).SingleAsync();
            Assert.EndsWith(".mpg", media.RelativePath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".ksc", media.LyricsRelativePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("KSC", media.LyricsFormat);
            Assert.Equal("测试歌", media.Song.Title);
            Assert.DoesNotContain(await database.MediaFiles.Select(x => x.RelativePath).ToListAsync(), x => x.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("song.MKV", MediaEntryKind.PlayableMedia)]
    [InlineData("song.mpg", MediaEntryKind.PlayableMedia)]
    [InlineData("song.MPEG", MediaEntryKind.PlayableMedia)]
    [InlineData("song.ksc", MediaEntryKind.LyricsSidecar)]
    [InlineData("bundle.RAR", MediaEntryKind.IgnoredArchive)]
    public void Format_policy_classifies_media_sidecars_and_archives(string path, MediaEntryKind expected) => Assert.Equal(expected, MediaFormatPolicy.Classify(path));

    [Fact]
    public async Task Scan_is_read_only_incremental_and_retains_missing_index()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "中文子目录"));
        var first = Path.Combine(root, "第一首.mkv");
        var second = Path.Combine(root, "中文子目录", "SECOND.MKV");
        await File.WriteAllTextAsync(first, "first"); await File.WriteAllTextAsync(second, "second"); await File.WriteAllTextAsync(Path.Combine(root, "ignore.txt"), "ignored");
        var original = await File.ReadAllTextAsync(first);
        try
        {
            await using var database = CreateDatabase();
            await database.Database.EnsureCreatedAsync();
            var source = new MediaSource { Name = "Fixture", RootPath = root, Availability = AvailabilityStatus.Available };
            database.MediaSources.Add(source); await database.SaveChangesAsync();
            var scanner = new MediaScanService(new EfMediaScanRepository(database), new FileSystemMediaFileEnumerator());
            var initial = await scanner.ScanAsync(source.Id);
            Assert.Equal(2, initial.Value.DiscoveredFiles); Assert.Equal(2, initial.Value.UpdatedFiles);
            var unchanged = await scanner.ScanAsync(source.Id);
            Assert.Equal(0, unchanged.Value.UpdatedFiles);
            Assert.Equal(original, await File.ReadAllTextAsync(first));
            File.Delete(first);
            var missing = await scanner.ScanAsync(source.Id);
            Assert.Equal(2, await database.MediaFiles.CountAsync());
            Assert.Equal(AvailabilityStatus.Offline, (await database.MediaFiles.SingleAsync(x => x.RelativePath == "第一首.mkv")).Availability);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Completed_directories_can_be_indexed_incrementally_without_replacing_existing_catalog()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-years-{Guid.NewGuid():N}");
        var year16 = Path.Combine(root, "16年"); var year17 = Path.Combine(root, "17年");
        Directory.CreateDirectory(year16); Directory.CreateDirectory(year17);
        await File.WriteAllTextAsync(Path.Combine(year16, "歌手甲-第一首-国语-流行.mkv"), "one");
        await File.WriteAllTextAsync(Path.Combine(year17, "歌手乙-第二首-国语-流行.mpg"), "two");
        try
        {
            await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
            var first = new MediaSource { Name = "16年", RootPath = year16, Availability = AvailabilityStatus.Available };
            var second = new MediaSource { Name = "17年", RootPath = year17, Availability = AvailabilityStatus.Available };
            database.MediaSources.AddRange(first, second); await database.SaveChangesAsync();
            var scanner = new MediaScanService(new EfMediaScanRepository(database), new FileSystemMediaFileEnumerator());

            Assert.True((await scanner.ScanAsync(first.Id)).IsSuccess);
            Assert.True((await scanner.ScanAsync(second.Id)).IsSuccess);

            Assert.Equal(2, await database.Songs.CountAsync());
            Assert.Equal(2, await database.MediaFiles.CountAsync());
            Assert.All(await database.MediaFiles.ToListAsync(), x => Assert.Equal(AvailabilityStatus.Available, x.Availability));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Enumeration_error_does_not_mark_existing_files_offline()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        database.MediaFiles.Add(new MediaFile { MediaSource = source, RelativePath = "kept.mkv", Song = new Song { Title = "Kept", NormalizedTitle = "kept" }, Availability = AvailabilityStatus.Available });
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new ErrorEnumerator());
        var result = await scanner.ScanAsync(source.Id);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Equal(AvailabilityStatus.Available, (await database.MediaFiles.SingleAsync()).Availability);
    }

    [Fact]
    public async Task Cancellation_persists_run_checkpoint()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new CancellingEnumerator(cancellation));
        var result = await scanner.ScanAsync(source.Id, cancellation.Token);
        Assert.Equal(ScanStatus.Cancelled, result.Value.Status);
        Assert.Equal("first.mkv", result.Value.CheckpointRelativePath);
        Assert.Equal(ScanStatus.Cancelled, (await database.ScanRuns.SingleAsync()).Status);
    }

    [Fact]
    public async Task New_file_probe_persists_duration_and_tracks()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-probe-{Guid.NewGuid():N}");
        var source = new MediaSource { Name = "Fixture", RootPath = root, Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new SingleFileEnumerator(), mediaProbe: new SuccessfulProbe());

        var result = await scanner.ScanAsync(source.Id);

        Assert.Equal(ScanStatus.Completed, result.Value.Status);
        var file = await database.MediaFiles.Include(x => x.Tracks).SingleAsync();
        Assert.NotNull(file.DurationSeconds);
        Assert.Equal(10.023, file.DurationSeconds.Value, 3);
        Assert.Equal(2, file.Tracks.Count);
        Assert.Contains(file.Tracks, x => x.Type == MediaTrackType.Audio && x.Title == "伴奏");
        Assert.Equal(1, file.TrackMapping?.BackingTrackId);
        Assert.False(file.TrackMapping?.IsManualOverride);
    }

    [Fact]
    public async Task Probe_failure_marks_file_unreadable_without_removing_index()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new SingleFileEnumerator(), mediaProbe: new FailedProbe());

        var result = await scanner.ScanAsync(source.Id);

        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Equal(1, await database.MediaFiles.CountAsync());
        var file = await database.MediaFiles.SingleAsync();
        Assert.Equal(AvailabilityStatus.Unreadable, file.Availability);
        Assert.Equal("media_probe.process_failed", file.LastErrorCode);
    }

    [Fact]
    public async Task Reprobe_does_not_overwrite_manual_track_mapping()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        var file = new MediaFile
        {
            MediaSource = source,
            Song = new Song { Title = "测试歌曲" },
            RelativePath = "测试歌曲.mkv",
            SizeBytes = 1,
            LastWriteTime = DateTimeOffset.UnixEpoch,
            Availability = AvailabilityStatus.Available,
            TrackMapping = new TrackMapping { BackingTrackId = 2, VocalTrackId = 1, IsManualOverride = true },
        };
        database.MediaFiles.Add(file);
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(new EfMediaScanRepository(database), new SingleFileEnumerator(), mediaProbe: new SuccessfulProbe());

        await scanner.ScanAsync(source.Id);

        database.ChangeTracker.Clear();
        var mapping = await database.TrackMappings.SingleAsync();
        Assert.True(mapping.IsManualOverride);
        Assert.Equal(2, mapping.BackingTrackId);
        Assert.Equal(1, mapping.VocalTrackId);
    }

    [Fact]
    public async Task New_file_uses_optional_nfo_metadata()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        database.MediaSources.Add(source);
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(
            new EfMediaScanRepository(database),
            new SingleFileEnumerator(),
            metadataReader: new NfoReader(),
            searchTextNormalizer: new ToolGoodSearchTextNormalizer());

        await scanner.ScanAsync(source.Id);

        var song = await database.Songs.Include(x => x.Artists).ThenInclude(x => x.Artist).SingleAsync();
        Assert.Equal("NFO 标题", song.Title);
        Assert.Equal("歌手乙", song.Artists.Single().Artist.Name);
        Assert.Equal(2026, song.Year);
        Assert.Equal("nfobiaoti", song.TitlePinyin);
        Assert.Equal("nfobt", song.TitleInitials);
    }

    [Fact]
    public async Task Existing_index_with_empty_search_keys_is_backfilled_without_touching_media()
    {
        await using var database = CreateDatabase();
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "Fixture", RootPath = "unused", Availability = AvailabilityStatus.Available };
        var song = new Song
        {
            Title = "夜曲",
            Availability = AvailabilityStatus.Available,
            Artists = [new SongArtist { Artist = new Artist { Name = "周杰伦" } }],
        };
        database.MediaFiles.Add(new MediaFile
        {
            MediaSource = source,
            Song = song,
            RelativePath = "测试歌曲.mkv",
            SizeBytes = 1024,
            LastWriteTime = DateTimeOffset.UnixEpoch,
            Availability = AvailabilityStatus.Available,
        });
        await database.SaveChangesAsync();
        var scanner = new MediaScanService(
            new EfMediaScanRepository(database),
            new SingleFileEnumerator(),
            searchTextNormalizer: new ToolGoodSearchTextNormalizer());

        var result = await scanner.ScanAsync(source.Id);

        Assert.Equal(1, result.Value.UpdatedFiles);
        Assert.Equal("yequ", song.TitlePinyin);
        Assert.Equal("zhoujielun", song.Artists.Single().Artist.Pinyin);
        Assert.Equal("zjl", song.Artists.Single().Artist.Initials);
    }

    private static StationDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        return new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
    }

    private sealed class ErrorEnumerator : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield(); yield return MediaEnumerationEntry.Error("blocked", "scan.directory_unreadable");
        }
    }

    private sealed class CancellingEnumerator(CancellationTokenSource cancellation) : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return MediaEnumerationEntry.File("first.mkv", 1, DateTimeOffset.UnixEpoch);
            cancellation.Cancel();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class SingleFileEnumerator : IMediaFileEnumerator
    {
        public async IAsyncEnumerable<MediaEnumerationEntry> EnumerateAsync(MediaSource source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return MediaEnumerationEntry.File("测试歌曲.mkv", 1024, DateTimeOffset.UnixEpoch);
        }
    }

    private sealed class SuccessfulProbe : IMediaProbe
    {
        public Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MediaProbeResult>.Success(new MediaProbeResult(10.023,
            [
                new(0, MediaTrackType.Video, "h264", null, null),
                new(1, MediaTrackType.Audio, "aac", "zho", "伴奏"),
            ])));
    }

    private sealed class FailedProbe : IMediaProbe
    {
        public Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MediaProbeResult>.Failure(new Error("media_probe.process_failed", "Probe failed.")));
    }

    private sealed class NfoReader : INfoMetadataReader
    {
        public Task<NfoReadResult> ReadForMediaAsync(string mediaPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new NfoReadResult(new NfoSongMetadata("NFO 标题", ["歌手乙"], "国语", "流行", 2026, "4K", null), []));
    }
}
