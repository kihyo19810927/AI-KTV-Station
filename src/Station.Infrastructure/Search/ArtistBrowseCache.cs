using System.Collections.Concurrent;
using Station.Application.Search;

namespace Station.Infrastructure.Search;

/// <summary>One immutable statistics snapshot per database, shared by desktop and API.</summary>
public sealed class ArtistBrowseCache(TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    public void Invalidate(string databaseKey) => entries.TryRemove(databaseKey, out _);
    public async Task<IReadOnlyList<ArtistBrowseItem>> GetAsync(string databaseKey,
        Func<CancellationToken, Task<IReadOnlyList<ArtistBrowseItem>>> load, CancellationToken token)
    {
        var entry = entries.GetOrAdd(databaseKey, _ => new Entry());
        await entry.Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (entry.Items is not null && clock.GetUtcNow() < entry.ExpiresAt) return entry.Items;
            var items = await load(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            entry.Items = items;
            entry.ExpiresAt = clock.GetUtcNow().AddSeconds(30);
            return items;
        }
        finally { entry.Gate.Release(); }
    }
    private sealed class Entry
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public IReadOnlyList<ArtistBrowseItem>? Items;
        public DateTimeOffset ExpiresAt;
    }
}
