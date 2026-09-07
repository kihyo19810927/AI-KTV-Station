using System.Globalization;
using System.Text.Json;
using Station.Application.Common;
using Station.Application.Media;
using Station.Domain.Models;

namespace Station.Infrastructure.Media;

public static class FfprobeJsonParser
{
    public static Result<MediaProbeResult> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var durationText = root.GetProperty("format").GetProperty("duration").GetString();
            if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration < 0)
                return Failure("media_probe.invalid_duration");
            var tracks = new List<MediaProbeTrack>();
            foreach (var stream in root.GetProperty("streams").EnumerateArray())
            {
                if (!TryMapType(stream.GetProperty("codec_type").GetString(), out var type)) continue;
                var tags = stream.TryGetProperty("tags", out var tagElement) ? tagElement : default;
                tracks.Add(new MediaProbeTrack(
                    stream.GetProperty("index").GetInt32(),
                    type,
                    stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "unknown" : "unknown",
                    TryGetTag(tags, "language"),
                    TryGetTag(tags, "title")));
            }
            return Result<MediaProbeResult>.Success(new MediaProbeResult(duration, tracks));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Failure("media_probe.invalid_json");
        }
    }

    private static string? TryGetTag(JsonElement tags, string name) => tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static bool TryMapType(string? value, out MediaTrackType type)
    {
        type = value switch { "video" => MediaTrackType.Video, "audio" => MediaTrackType.Audio, "subtitle" => MediaTrackType.Subtitle, _ => default };
        return value is "video" or "audio" or "subtitle";
    }
    private static Result<MediaProbeResult> Failure(string code) => Result<MediaProbeResult>.Failure(new Error(code, "Media metadata could not be read."));
}
