using Station.Application.Playback;
using Station.Application.Rooms;

namespace Station.Server.Playback;

public sealed class RoomPlaybackHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RoomPlaybackHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Guid? activeRoomId = null;
        CancellationTokenSource? roomCancellation = null;
        Task? roomTask = null;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var roomId = await GetOpenRoomIdAsync(stoppingToken);
                if (roomId != activeRoomId || roomTask?.IsCompleted == true)
                {
                    await StopRoomAsync(roomCancellation, roomTask);
                    roomCancellation?.Dispose();
                    roomCancellation = null;
                    roomTask = null;
                    activeRoomId = roomId;

                    if (roomId is not null)
                    {
                        roomCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        roomTask = RunRoomAsync(roomId.Value, roomCancellation.Token);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await StopRoomAsync(roomCancellation, roomTask);
            roomCancellation?.Dispose();
        }
    }

    private async Task<Guid?> GetOpenRoomIdAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<RoomLifecycleService>()
            .GetCurrentAsync(cancellationToken);
        return result.IsSuccess ? result.Value?.Id : null;
    }

    private async Task RunRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<QueuePlaybackOrchestrator>()
                .RunAsync(roomId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Playback coordinator stopped for room {RoomId}.", roomId);
        }
    }

    private static async Task StopRoomAsync(CancellationTokenSource? cancellation, Task? task)
    {
        if (cancellation is null || task is null) return;
        cancellation.Cancel();
        try { await task; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }
}
