using Station.Application.Common;

namespace Station.Application.Playback;

public sealed record PlaybackStartupRecoveryResult(int RequeuedItems, int ClosedHistories);

public interface IPlaybackStartupRecoveryStore
{
    Task<PlaybackStartupRecoveryResult> RecoverInterruptedAsync(DateTimeOffset recoveredAt, CancellationToken cancellationToken = default);
}

public sealed class PlaybackStartupRecoveryService(IPlaybackStartupRecoveryStore store, TimeProvider clock)
{
    public async Task<Result<PlaybackStartupRecoveryResult>> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var recovered = await store.RecoverInterruptedAsync(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return Result<PlaybackStartupRecoveryResult>.Success(recovered);
    }
}
