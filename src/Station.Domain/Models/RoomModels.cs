namespace Station.Domain.Models;

public sealed class RoomSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JoinCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Open;
    public int? OpenSlot { get; set; }
    public int MaxQueuedSongsPerGuest { get; set; } = 100;
    public List<Guest> Guests { get; set; } = [];
    public List<QueueItem> Queue { get; set; } = [];
}

public sealed class Guest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomSessionId { get; set; }
    public RoomSession RoomSession { get; set; } = null!;
    public string Nickname { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsHost { get; set; }
}

public sealed class QueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomSessionId { get; set; }
    public RoomSession RoomSession { get; set; } = null!;
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public Guid RequestedByGuestId { get; set; }
    public Guest RequestedByGuest { get; set; } = null!;
    public long Position { get; set; }
    public QueueItemStatus Status { get; set; } = QueueItemStatus.Waiting;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class Favorite
{
    public Guid GuestId { get; set; }
    public Guest Guest { get; set; } = null!;
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlayHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomSessionId { get; set; }
    public Guid SongId { get; set; }
    public Guid? QueueItemId { get; set; }
    public PlaybackOutcome Outcome { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>Long-lived household identity.  It deliberately has no room or host role.</summary>
public sealed class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PinHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public bool IsArchived { get; set; }
    public List<ProfileDevice> Devices { get; set; } = [];
    public List<ProfilePlaylist> Playlists { get; set; } = [];
}

public sealed class ProfileDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class ProfilePlaylist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ProfilePlaylistKind Kind { get; set; } = ProfilePlaylistKind.Custom;
    public bool IsFamilyShared { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ProfilePlaylistItem> Items { get; set; } = [];
}

public sealed class ProfilePlaylistItem
{
    public Guid ProfilePlaylistId { get; set; }
    public ProfilePlaylist ProfilePlaylist { get; set; } = null!;
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public long Position { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
