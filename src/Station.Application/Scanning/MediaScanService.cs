using Station.Application.Common;
using Station.Application.Media;
using Station.Application.Metadata;
using Station.Application.Playback;
using Station.Application.Search;
using Station.Domain.Models;

namespace Station.Application.Scanning;

public sealed class MediaScanService(
    IMediaScanRepository repository,
    IMediaFileEnumerator fileEnumerator,
    IMediaFilenameParser? filenameParser = null,
    IMediaProbe? mediaProbe = null,
    INfoMetadataReader? metadataReader = null,
    ISearchTextNormalizer? searchTextNormalizer = null,
    AudioTrackClassifier? audioTrackClassifier = null) : IMediaScanRunner
{
    private const int CheckpointBatchSize = 100;

    public async Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, CancellationToken cancellationToken = default)
    {
        return await ScanCoreAsync(mediaSourceId, Guid.NewGuid(), null, cancellationToken);
    }

    public async Task<Result<ScanRun>> ScanAsync(
        Guid mediaSourceId,
        Guid scanRunId,
        IProgress<MediaScanProgress> progress,
        CancellationToken cancellationToken = default)
    {
        return await ScanCoreAsync(mediaSourceId, scanRunId, progress, cancellationToken);
    }

    private async Task<Result<ScanRun>> ScanCoreAsync(
        Guid mediaSourceId,
        Guid scanRunId,
        IProgress<MediaScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var source = await repository.FindSourceAsync(mediaSourceId, cancellationToken);
        if (source is null) return Result<ScanRun>.Failure(new Error("scan.source_not_found", "Media source was not found."));
        if (!source.IsEnabled) return Result<ScanRun>.Failure(new Error("scan.source_disabled", "Media source is disabled."));

        var run = new ScanRun { Id = scanRunId, MediaSourceId = source.Id, Status = ScanStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        await repository.AddRunAsync(run, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        Report(run, progress);
        var existing = (await repository.ListFilesAsync(source.Id, cancellationToken)).ToDictionary(x => Normalize(x.RelativePath), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lyricsSidecars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await foreach (var entry in fileEnumerator.EnumerateAsync(source, cancellationToken))
            {
                run.DiscoveredFiles++;
                run.CheckpointRelativePath = entry.RelativePath;
                if (entry.IsError) { run.ErrorCount++; run.ErrorSummary = entry.ErrorCode; continue; }
                var relativePath = Normalize(entry.RelativePath);
                if (entry.Kind == MediaEntryKind.IgnoredArchive) continue;
                if (entry.Kind == MediaEntryKind.LyricsSidecar)
                {
                    lyricsSidecars[WithoutExtension(relativePath)] = relativePath;
                    continue;
                }
                seen.Add(relativePath);
                var requiresProbe = false;
                var isNew = false;
                if (!existing.TryGetValue(relativePath, out var file))
                {
                    var filenameMetadata = (filenameParser ?? new KtvFilenameParser()).Parse(relativePath);
                    var mediaPath = Path.Combine(source.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var nfo = metadataReader is null
                        ? NfoReadResult.Missing
                        : await metadataReader.ReadForMediaAsync(mediaPath, cancellationToken);
                    var metadata = SongMetadataResolver.Resolve(filenameMetadata, nfo);
                    file = new MediaFile
                    {
                        MediaSourceId = source.Id,
                        RelativePath = relativePath,
                        SizeBytes = entry.SizeBytes!.Value,
                        LastWriteTime = entry.LastWriteTime!.Value,
                        Availability = AvailabilityStatus.Available,
                        Song = new Song
                        {
                            Title = metadata.Title,
                            Language = metadata.Language,
                            Category = metadata.Category,
                            Year = metadata.Year,
                            Quality = metadata.Quality,
                            Availability = AvailabilityStatus.Available,
                            Artists = metadata.Artists.Select((artist, order) => CreateArtist(artist, order)).ToList(),
                        },
                    };
                    await repository.AddFileAsync(file, cancellationToken);
                    existing.Add(relativePath, file);
                    run.UpdatedFiles++;
                    requiresProbe = true;
                    isNew = true;
                }
                else if (file.SizeBytes != entry.SizeBytes || file.LastWriteTime != entry.LastWriteTime || file.Availability != AvailabilityStatus.Available)
                {
                    file.SizeBytes = entry.SizeBytes!.Value;
                    file.LastWriteTime = entry.LastWriteTime!.Value;
                    file.Availability = AvailabilityStatus.Available;
                    file.LastErrorCode = null;
                    run.UpdatedFiles++;
                    requiresProbe = true;
                }
                if (requiresProbe && mediaProbe is not null)
                    await ProbeAsync(source, file, run, cancellationToken);
                if (SongSearchKeyUpdater.Update(file.Song, searchTextNormalizer ?? new InvariantSearchTextNormalizer()) && !isNew)
                    run.UpdatedFiles++;
                if (run.DiscoveredFiles % CheckpointBatchSize == 0) await repository.SaveChangesAsync(cancellationToken);
                Report(run, progress);
            }

            foreach (var file in existing.Values)
            {
                var lyrics = lyricsSidecars.GetValueOrDefault(WithoutExtension(Normalize(file.RelativePath)));
                if (lyrics is not null && !string.Equals(file.LyricsRelativePath, lyrics, StringComparison.OrdinalIgnoreCase))
                {
                    file.LyricsRelativePath = lyrics;
                    file.LyricsFormat = "KSC";
                    run.UpdatedFiles++;
                }
                else if (lyrics is null && run.ErrorCount == 0 && file.LyricsRelativePath is not null)
                {
                    file.LyricsRelativePath = null;
                    file.LyricsFormat = null;
                    run.UpdatedFiles++;
                }
            }

            if (run.ErrorCount == 0)
            {
                foreach (var file in existing.Values.Where(x => !seen.Contains(Normalize(x.RelativePath)) && x.Availability != AvailabilityStatus.Offline))
                {
                    file.Availability = AvailabilityStatus.Offline;
                    file.LastErrorCode = "media_file.not_seen";
                    run.UpdatedFiles++;
                }
            }
            run.Status = ScanStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            source.LastScanAt = run.CompletedAt;
            source.Availability = run.ErrorCount == 0 ? AvailabilityStatus.Available : AvailabilityStatus.Unknown;
            await repository.SaveChangesAsync(cancellationToken);
            Report(run, progress);
            return Result<ScanRun>.Success(run);
        }
        catch (OperationCanceledException)
        {
            run.Status = ScanStatus.Cancelled;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(CancellationToken.None);
            Report(run, progress);
            return Result<ScanRun>.Success(run);
        }
        catch (Exception exception)
        {
            run.Status = ScanStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.ErrorCount++;
            run.ErrorSummary = exception.GetType().Name;
            try { await repository.SaveChangesAsync(CancellationToken.None); }
            catch (Exception) { /* Preserve the original scan failure. */ }
            Report(run, progress);
            throw;
        }
    }

    private static void Report(ScanRun run, IProgress<MediaScanProgress>? progress) =>
        progress?.Report(new MediaScanProgress(run.Id, run.Status, run.DiscoveredFiles, run.UpdatedFiles, run.ErrorCount));

    private static SongArtist CreateArtist(string name, int order) => new() { Order = order, Artist = new Artist { Name = name } };

    private async Task ProbeAsync(MediaSource source, MediaFile file, ScanRun run, CancellationToken cancellationToken)
    {
        var relativePath = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        var result = await mediaProbe!.ProbeAsync(Path.Combine(source.RootPath, relativePath), cancellationToken);
        if (!result.IsSuccess)
        {
            file.Availability = AvailabilityStatus.Unreadable;
            file.LastErrorCode = result.Error.Code;
            run.ErrorCount++;
            run.ErrorSummary = result.Error.Code;
            return;
        }

        file.DurationSeconds = result.Value.DurationSeconds;
        file.Tracks.Clear();
        foreach (var track in result.Value.Tracks)
        {
            var mediaTrack = new MediaTrack
            {
                MediaFile = file,
                StreamId = track.StreamId,
                Type = track.Type,
                Codec = track.Codec,
                Language = track.Language,
                Title = track.Title,
            };
            file.Tracks.Add(mediaTrack);
            await repository.AddTrackAsync(mediaTrack, cancellationToken);
        }
        if (file.TrackMapping?.IsManualOverride != true)
        {
            var automatic = (audioTrackClassifier ?? new AudioTrackClassifier())
                .CreateAutomaticMapping(file.Id, file.Tracks, file.TrackMapping?.DefaultSubtitleTrackId);
            if (file.TrackMapping is null)
            {
                file.TrackMapping = automatic;
            }
            else
            {
                file.TrackMapping.BackingTrackId = automatic.BackingTrackId;
                file.TrackMapping.VocalTrackId = automatic.VocalTrackId;
                file.TrackMapping.IsManualOverride = false;
            }
        }
        file.Availability = AvailabilityStatus.Available;
        file.LastErrorCode = null;
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/');
    private static string WithoutExtension(string relativePath) => Path.ChangeExtension(relativePath, null) ?? relativePath;
}
