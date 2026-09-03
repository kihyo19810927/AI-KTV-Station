namespace Station.Domain.Models;

public sealed class RoomSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JoinCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Open;
    public int MaxQueuedSongsPerGuest { get; set; } = 10;
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
