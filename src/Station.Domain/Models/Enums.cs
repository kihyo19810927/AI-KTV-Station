namespace Station.Domain.Models;

public enum AvailabilityStatus { Unknown, Available, Offline, Unreadable }
public enum MediaTrackType { Video, Audio, Subtitle }
public enum TrackPurpose { Unknown, Backing, Vocal, DefaultSubtitle }
public enum RoomStatus { Open, Closed }
public enum QueueItemStatus { Probing, ProbeFailed, Waiting, Preparing, Playing, Paused, Completed, Skipped, Failed }
public enum PlaybackOutcome { Completed, Skipped, Failed }
public enum ScanStatus { Pending, Running, Completed, Cancelled, Failed }
