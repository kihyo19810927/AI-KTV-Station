using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Media;

public interface IMediaProbe
{
    Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default);
}

public sealed record MediaProbeResult(double DurationSeconds, IReadOnlyList<MediaProbeTrack> Tracks);
public sealed record MediaProbeTrack(int StreamId, MediaTrackType Type, string Codec, string? Language, string? Title);
