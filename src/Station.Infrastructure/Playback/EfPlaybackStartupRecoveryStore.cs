using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Playback;

public sealed class EfPlaybackStartupRecoveryStore(StationDbContext database) : IPlaybackStartupRecoveryStore
{
    public async Task<PlaybackStartupRecoveryResult> RecoverInterruptedAsync(DateTimeOffset recoveredAt, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var interrupted = await database.QueueItems
            .Where(x => x.Status == QueueItemStatus.Preparing || x.Status == QueueItemStatus.Playing || x.Status == QueueItemStatus.Paused)
            .ToListAsync(cancellationToken);
        var interruptedIds = interrupted.Select(x => x.Id).ToArray();
        var histories = interruptedIds.Length == 0
            ? []
            : await database.PlayHistory.Where(x => x.QueueItemId != null && interruptedIds.Contains(x.QueueItemId.Value) && x.EndedAt == null).ToListAsync(cancellationToken);

        foreach (var item in interrupted)
        {
            item.Status = QueueItemStatus.Waiting;
            item.CompletedAt = null;
        }
        foreach (var history in histories)
        {
            history.Outcome = PlaybackOutcome.Failed;
            history.EndedAt = recoveredAt;
            history.ErrorCode = "playback.interrupted_by_restart";
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(interrupted.Count, histories.Count);
    }
}
