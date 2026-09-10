using Station.Application.Common;
using Station.Application.Configuration;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    AudioTrackClassifier? audioTrackClassifier = null,
    ScanOptions? scanOptions = null,
    ISongSearchIndex? searchIndex = null) : IMediaScanRunner
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
        var existing = new Dictionary<string, MediaFile>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lyricsSidecars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var settings = scanOptions ?? new ScanOptions();
        var basicOnly = settings.BasicIndexOnly;
        var readNfo = settings.ReadNfo;
        var concurrency = Math.Clamp(settings.ProbeConcurrency, 1, 4);
        var pendingProbes = new List<MediaFile>();
        var indexedSongs = new HashSet<Guid>();

        try
        {
            foreach (var stored in await repository.ListFilesAsync(source.Id, cancellationToken)) existing.Add(Normalize(stored.RelativePath), stored);
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
                var isNew = false;
                var legacySuccess = existing.TryGetValue(relativePath, out var previous) && previous.ProbeFingerprint is null &&
                    previous.DurationSeconds is not null && previous.LastErrorCode is null &&
                    previous.SizeBytes == entry.SizeBytes && previous.LastWriteTime == entry.LastWriteTime;
                if (!existing.TryGetValue(relativePath, out var file))
                {
                    var filenameMetadata = (filenameParser ?? new KtvFilenameParser()).Parse(relativePath);
                    var mediaPath = Path.Combine(source.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var nfo = metadataReader is null || !readNfo
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
                    isNew = true;
                }
                else if (file.SizeBytes != entry.SizeBytes || file.LastWriteTime != entry.LastWriteTime || file.Availability != AvailabilityStatus.Available)
                {
                    // Persist pending state before updating the observed fingerprint, including legacy rows.
                    if (!legacySuccess && file.ProbeFingerprint is null) file.ProbeFingerprint = string.Empty;
                    file.SizeBytes = entry.SizeBytes!.Value;
                    file.LastWriteTime = entry.LastWriteTime!.Value;
                    file.Availability = AvailabilityStatus.Available;
                    file.LastErrorCode = null;
                    run.UpdatedFiles++;
                }
                // Adopt successful legacy metadata only when its original size/time were unchanged.
                if (legacySuccess) file.ProbeFingerprint = Fingerprint(source, file);
                if (file.ProbeFingerprint != Fingerprint(source, file)) pendingProbes.Add(file);
                else run.CachedFiles++;
                run.IndexedFiles++;
                indexedSongs.Add(file.Song.Id);
                if (SongSearchKeyUpdater.Update(file.Song, searchTextNormalizer ?? new InvariantSearchTextNormalizer()) && !isNew)
                    run.UpdatedFiles++;
                if (run.IndexedFiles % CheckpointBatchSize == 0)
                {
                    await repository.SaveChangesAsync(cancellationToken);
                    if (searchIndex is not null) await searchIndex.UpsertAsync(indexedSongs, cancellationToken);
                    indexedSongs.Clear();
                }
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
                    indexedSongs.Add(file.Song.Id);
                    file.LastErrorCode = "media_file.not_seen";
                    run.UpdatedFiles++;
                }
            }
            await repository.SaveChangesAsync(cancellationToken);
            if (searchIndex is not null) await searchIndex.UpsertAsync(indexedSongs, cancellationToken);
            indexedSongs.Clear();
            if (!basicOnly && mediaProbe is not null)
            {
                run.Phase = "Probing";
                Report(run, progress);
                await ProbePendingAsync(source, pendingProbes, run, concurrency, progress, cancellationToken);
            }
            run.Phase = basicOnly ? "BasicIndexComplete" : "Complete";
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
            if (searchIndex is not null) await searchIndex.UpsertAsync(indexedSongs, CancellationToken.None);
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
        progress?.Report(new MediaScanProgress(run.Id, run.Status, run.DiscoveredFiles, run.UpdatedFiles, run.ErrorCount,
            run.IndexedFiles, run.ProbedFiles, run.CachedFiles, run.ProbeAttempts == 0 ? 0 : run.ProbeMilliseconds / run.ProbeAttempts, run.Phase));

    private static SongArtist CreateArtist(string name, int order) => new() { Order = order, Artist = new Artist { Name = name } };

    public static string Fingerprint(MediaSource source, MediaFile file) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{source.RootPath.Replace('\\', '/').TrimEnd('/').ToUpperInvariant()}/{Normalize(file.RelativePath).ToUpperInvariant()}\n{file.SizeBytes}\n{file.LastWriteTime.UtcTicks}")));

    private async Task ProbePendingAsync(MediaSource source, List<MediaFile> files, ScanRun run, int concurrency,
        IProgress<MediaScanProgress>? progress, CancellationToken cancellationToken)
    {
        using var workersCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var active = new List<Task<(MediaFile File, Result<MediaProbeResult> Result, double Milliseconds)>>();
        var next = 0;
        try
        {
            while (next < files.Count || active.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (active.Count < concurrency && next < files.Count)
                    active.Add(ProbeOneAsync(source, files[next++], workersCancellation.Token));
                var finished = await Task.WhenAny(active);
                active.Remove(finished);
                var completed = await finished;
                // Workers never touch the tracked graph or DbContext. Apply results on this one consumer.
                await ApplyProbeAsync(completed.File, completed.Result, run, CancellationToken.None);
                run.ProbeAttempts++;
                run.ProbeMilliseconds += completed.Milliseconds;
                if (completed.Result.IsSuccess)
                {
                    completed.File.ProbeFingerprint = Fingerprint(source, completed.File);
                    run.ProbedFiles++;
                }
                await repository.SaveChangesAsync(CancellationToken.None);
                Report(run, progress);
            }
        }
        finally
        {
            await workersCancellation.CancelAsync();
            try { await Task.WhenAll(active); } catch (OperationCanceledException) { }
        }
    }

    private async Task<(MediaFile, Result<MediaProbeResult>, double)> ProbeOneAsync(MediaSource source, MediaFile file, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        Result<MediaProbeResult> result;
        try { result = await mediaProbe!.ProbeAsync(Path.Combine(source.RootPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)), token); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { result = Result<MediaProbeResult>.Failure(new Error("media_probe.unhandled_error", "Media probe failed.")); }
        return (file, result, timer.Elapsed.TotalMilliseconds);
    }

    private async Task ApplyProbeAsync(MediaFile file, Result<MediaProbeResult> result, ScanRun run, CancellationToken cancellationToken)
    {
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
