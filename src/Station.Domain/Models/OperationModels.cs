namespace Station.Domain.Models;

public sealed class ScanRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MediaSourceId { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long DiscoveredFiles { get; set; }
    public long UpdatedFiles { get; set; }
    public long ErrorCount { get; set; }
    public string? ErrorSummary { get; set; }
    public string? CheckpointRelativePath { get; set; }
}

public sealed class PlaybackError
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MediaFileId { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
    public string DiagnosticSummary { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
