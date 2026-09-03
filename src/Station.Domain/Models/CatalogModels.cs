namespace Station.Domain.Models;

public sealed class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Category { get; set; }
    public int? Year { get; set; }
    public string? Quality { get; set; }
    public AvailabilityStatus Availability { get; set; }
    public List<SongArtist> Artists { get; set; } = [];
    public List<MediaFile> MediaFiles { get; set; } = [];
}

public sealed class Artist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Pinyin { get; set; }
    public string? Initials { get; set; }
    public List<SongArtist> Songs { get; set; } = [];
}

public sealed class SongArtist
{
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;
    public int Order { get; set; }
    public string Relationship { get; set; } = "Primary";
}

public sealed class MediaSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public AvailabilityStatus Availability { get; set; }
    public DateTimeOffset? LastScanAt { get; set; }
    public List<MediaFile> Files { get; set; } = [];
}

public sealed class MediaFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public Guid MediaSourceId { get; set; }
    public MediaSource MediaSource { get; set; } = null!;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset LastWriteTime { get; set; }
    public double? DurationSeconds { get; set; }
    public AvailabilityStatus Availability { get; set; }
    public string? LastErrorCode { get; set; }
    public List<MediaTrack> Tracks { get; set; } = [];
    public TrackMapping? TrackMapping { get; set; }
}

public sealed class MediaTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MediaFileId { get; set; }
    public MediaFile MediaFile { get; set; } = null!;
    public int StreamId { get; set; }
    public MediaTrackType Type { get; set; }
    public string? Language { get; set; }
    public string? Title { get; set; }
    public string? Codec { get; set; }
}

public sealed class TrackMapping
{
    public Guid MediaFileId { get; set; }
    public MediaFile MediaFile { get; set; } = null!;
    public int? BackingTrackId { get; set; }
    public int? VocalTrackId { get; set; }
    public int? DefaultSubtitleTrackId { get; set; }
    public bool IsManualOverride { get; set; }
}
