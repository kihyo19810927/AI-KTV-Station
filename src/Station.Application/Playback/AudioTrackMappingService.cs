using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Playback;

public enum TrackMappingSource { Automatic, Manual }

public sealed record TrackRoleCandidate(int StreamId, TrackPurpose Purpose, int Confidence, string Reason);

public sealed record AudioTrackMappingResolution(
    int? BackingTrackId,
    int? VocalTrackId,
    TrackMappingSource Source,
    IReadOnlyList<TrackRoleCandidate> Candidates);

public interface ITrackMappingRepository
{
    Task<TrackMapping?> FindAsync(Guid mediaFileId, CancellationToken cancellationToken = default);
    Task SaveAsync(TrackMapping mapping, CancellationToken cancellationToken = default);
}

public sealed class AudioTrackClassifier
{
    private const int AutomaticSelectionThreshold = 80;
    private static readonly (string Text, int Score)[] BackingKeywords =
    [
        ("伴奏", 100), ("instrumental", 95), ("off vocal", 95), ("karaoke", 90), ("纯音乐", 85), ("純音樂", 85),
    ];
    private static readonly (string Text, int Score)[] VocalKeywords =
    [
        ("原唱", 100), ("人声", 95), ("人聲", 95), ("lead vocal", 95), ("vocal", 85),
    ];

    public IReadOnlyList<TrackRoleCandidate> Classify(IReadOnlyList<MediaTrack> tracks)
    {
        var audio = tracks.Where(x => x.Type == MediaTrackType.Audio).OrderBy(x => x.StreamId).ToArray();
        var candidates = new List<TrackRoleCandidate>();
        foreach (var track in audio)
        {
            var title = track.Title?.Trim().ToLowerInvariant() ?? string.Empty;
            AddBestKeywordCandidate(candidates, track.StreamId, TrackPurpose.Backing, title, BackingKeywords);
            if (!title.Contains("off vocal", StringComparison.Ordinal))
                AddBestKeywordCandidate(candidates, track.StreamId, TrackPurpose.Vocal, title, VocalKeywords);
        }
        if (audio.Length == 2)
        {
            if (!candidates.Any(x => x.StreamId == audio[0].StreamId && x.Purpose == TrackPurpose.Backing))
                candidates.Add(new TrackRoleCandidate(audio[0].StreamId, TrackPurpose.Backing, 25, "two_track_order"));
            if (!candidates.Any(x => x.StreamId == audio[1].StreamId && x.Purpose == TrackPurpose.Vocal))
                candidates.Add(new TrackRoleCandidate(audio[1].StreamId, TrackPurpose.Vocal, 25, "two_track_order"));
        }
        return candidates.OrderByDescending(x => x.Confidence).ThenBy(x => x.StreamId).ToArray();
    }

    public TrackMapping CreateAutomaticMapping(Guid mediaFileId, IReadOnlyList<MediaTrack> tracks, int? defaultSubtitleTrackId = null)
    {
        var candidates = Classify(tracks);
        var backing = Best(candidates, TrackPurpose.Backing);
        var vocal = Best(candidates, TrackPurpose.Vocal);
        if (backing == vocal) vocal = null;
        return new TrackMapping
        {
            MediaFileId = mediaFileId,
            BackingTrackId = backing,
            VocalTrackId = vocal,
            DefaultSubtitleTrackId = defaultSubtitleTrackId,
            IsManualOverride = false,
        };
    }

    private static void AddBestKeywordCandidate(
        ICollection<TrackRoleCandidate> candidates,
        int streamId,
        TrackPurpose purpose,
        string title,
        IEnumerable<(string Text, int Score)> keywords)
    {
        var match = keywords.Where(x => title.Contains(x.Text, StringComparison.Ordinal)).OrderByDescending(x => x.Score).FirstOrDefault();
        if (match.Text is not null) candidates.Add(new TrackRoleCandidate(streamId, purpose, match.Score, $"title:{match.Text}"));
    }

    private static int? Best(IEnumerable<TrackRoleCandidate> candidates, TrackPurpose purpose) =>
        candidates.Where(x => x.Purpose == purpose && x.Confidence >= AutomaticSelectionThreshold)
            .OrderByDescending(x => x.Confidence).ThenBy(x => x.StreamId).Select(x => (int?)x.StreamId).FirstOrDefault();
}

public sealed class AudioTrackMappingService(ITrackMappingRepository repository, AudioTrackClassifier classifier)
{
    public async Task<Result<AudioTrackMappingResolution>> ResolveAsync(
        Guid mediaFileId,
        IReadOnlyList<MediaTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId == Guid.Empty) return InvalidMediaFile();
        ArgumentNullException.ThrowIfNull(tracks);
        var candidates = classifier.Classify(tracks);
        var stored = await repository.FindAsync(mediaFileId, cancellationToken).ConfigureAwait(false);
        if (stored is { IsManualOverride: true } && ReferencesAvailable(stored, tracks))
            return Result<AudioTrackMappingResolution>.Success(new(stored.BackingTrackId, stored.VocalTrackId, TrackMappingSource.Manual, candidates));

        var automatic = classifier.CreateAutomaticMapping(mediaFileId, tracks, stored?.DefaultSubtitleTrackId);
        await repository.SaveAsync(automatic, cancellationToken).ConfigureAwait(false);
        return Result<AudioTrackMappingResolution>.Success(new(automatic.BackingTrackId, automatic.VocalTrackId, TrackMappingSource.Automatic, candidates));
    }

    public async Task<Result<AudioTrackMappingResolution>> SaveManualOverrideAsync(
        Guid mediaFileId,
        IReadOnlyList<MediaTrack> tracks,
        int? backingTrackId,
        int? vocalTrackId,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId == Guid.Empty) return InvalidMediaFile();
        ArgumentNullException.ThrowIfNull(tracks);
        if (backingTrackId is null && vocalTrackId is null)
            return Result<AudioTrackMappingResolution>.Failure(new Error("track_mapping.empty", "At least one audio role must be selected."));
        if (backingTrackId is not null && backingTrackId == vocalTrackId)
            return Result<AudioTrackMappingResolution>.Failure(new Error("track_mapping.duplicate", "Backing and vocal roles must use different tracks."));
        var audioIds = tracks.Where(x => x.Type == MediaTrackType.Audio).Select(x => x.StreamId).ToHashSet();
        if (backingTrackId is { } backing && !audioIds.Contains(backing) || vocalTrackId is { } vocal && !audioIds.Contains(vocal))
            return Result<AudioTrackMappingResolution>.Failure(new Error("track_mapping.audio_track_not_found", "A selected audio track is unavailable."));

        var existing = await repository.FindAsync(mediaFileId, cancellationToken).ConfigureAwait(false);
        var mapping = new TrackMapping
        {
            MediaFileId = mediaFileId,
            BackingTrackId = backingTrackId,
            VocalTrackId = vocalTrackId,
            DefaultSubtitleTrackId = existing?.DefaultSubtitleTrackId,
            IsManualOverride = true,
        };
        await repository.SaveAsync(mapping, cancellationToken).ConfigureAwait(false);
        return Result<AudioTrackMappingResolution>.Success(new(backingTrackId, vocalTrackId, TrackMappingSource.Manual, classifier.Classify(tracks)));
    }

    private static bool ReferencesAvailable(TrackMapping mapping, IReadOnlyList<MediaTrack> tracks)
    {
        var audioIds = tracks.Where(x => x.Type == MediaTrackType.Audio).Select(x => x.StreamId).ToHashSet();
        return (mapping.BackingTrackId is null || audioIds.Contains(mapping.BackingTrackId.Value)) &&
               (mapping.VocalTrackId is null || audioIds.Contains(mapping.VocalTrackId.Value));
    }

    private static Result<AudioTrackMappingResolution> InvalidMediaFile() =>
        Result<AudioTrackMappingResolution>.Failure(new Error("track_mapping.invalid_media_file", "Media file id is required."));
}
