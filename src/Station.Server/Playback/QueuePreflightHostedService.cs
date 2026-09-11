using Station.Application.Playback;
using Station.Application.Rooms;

namespace Station.Server.Playback;

public sealed class QueuePreflightHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<QueuePreflightHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var room = await scope.ServiceProvider.GetRequiredService<RoomLifecycleService>().GetCurrentAsync(stoppingToken);
                var worked = room.IsSuccess && room.Value is not null
                    && await scope.ServiceProvider.GetRequiredService<IQueuePreflightService>()
                        .ProbeNextWaitingAsync(room.Value.Id, stoppingToken);
                if (!worked) await Task.Delay(TimeSpan.FromMilliseconds(500), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Queued media preflight failed; playback ordering is preserved.");
                await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, stoppingToken);
            }
        }
    }
}
