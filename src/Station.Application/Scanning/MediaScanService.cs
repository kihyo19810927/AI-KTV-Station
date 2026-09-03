using Station.Application.Common;
using Station.Application.Metadata;
using Station.Domain.Models;

namespace Station.Application.Scanning;

public sealed class MediaScanService(IMediaScanRepository repository, IMediaFileEnumerator fileEnumerator, IMediaFilenameParser? filenameParser = null)
{
    private const int CheckpointBatchSize = 100;

    public async Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, CancellationToken cancellationToken = default)
    {
        var source = await repository.FindSourceAsync(mediaSourceId, cancellationToken);
        if (source is null) return Result<ScanRun>.Failure(new Error("scan.source_not_found", "Media source was not found."));
        if (!source.IsEnabled) return Result<ScanRun>.Failure(new Error("scan.source_disabled", "Media source is disabled."));

        var run = new ScanRun { MediaSourceId = source.Id, Status = ScanStatus.Running, CreatedAt = DateTimeOffset.UtcNow };
        await repository.AddRunAsync(run, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var existing = (await repository.ListFilesAsync(source.Id, cancellationToken)).ToDictionary(x => Normalize(x.RelativePath), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await foreach (var entry in fileEnumerator.EnumerateAsync(source, cancellationToken))
            {
                run.DiscoveredFiles++;
                run.CheckpointRelativePath = entry.RelativePath;
                if (entry.IsError) { run.ErrorCount++; run.ErrorSummary = entry.ErrorCode; continue; }
                var relativePath = Normalize(entry.RelativePath);
                seen.Add(relativePath);
                if (!existing.TryGetValue(relativePath, out var file))
                {
                    var metadata = (filenameParser ?? new KtvFilenameParser()).Parse(relativePath);
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
                            NormalizedTitle = metadata.Title.Normalize(),
                            Language = metadata.Language,
                            Category = metadata.Category,
                            Quality = metadata.Quality,
                            Availability = AvailabilityStatus.Available,
                            Artists = metadata.ArtistCandidates.Select((artist, order) => new SongArtist
                            {
                                Order = order,
                                Artist = new Artist { Name = artist, NormalizedName = artist.Normalize() },
                            }).ToList(),
                        },
                    };
                    await repository.AddFileAsync(file, cancellationToken);
                    existing.Add(relativePath, file);
                    run.UpdatedFiles++;
                }
                else if (file.SizeBytes != entry.SizeBytes || file.LastWriteTime != entry.LastWriteTime || file.Availability != AvailabilityStatus.Available)
                {
                    file.SizeBytes = entry.SizeBytes!.Value;
                    file.LastWriteTime = entry.LastWriteTime!.Value;
                    file.Availability = AvailabilityStatus.Available;
                    file.LastErrorCode = null;
                    run.UpdatedFiles++;
                }
                if (run.DiscoveredFiles % CheckpointBatchSize == 0) await repository.SaveChangesAsync(cancellationToken);
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
            return Result<ScanRun>.Success(run);
        }
        catch (OperationCanceledException)
        {
            run.Status = ScanStatus.Cancelled;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(CancellationToken.None);
            return Result<ScanRun>.Success(run);
        }
        catch (Exception exception)
        {
            run.Status = ScanStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.ErrorCount++;
            run.ErrorSummary = exception.GetType().Name;
            await repository.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/');
}
