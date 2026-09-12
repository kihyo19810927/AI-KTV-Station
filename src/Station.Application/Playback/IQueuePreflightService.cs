namespace Station.Application.Playback;

public interface IQueuePreflightService
{
    Task<bool> ProbeNextWaitingAsync(Guid roomId, CancellationToken cancellationToken = default);
}
