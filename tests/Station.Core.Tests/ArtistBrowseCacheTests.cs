using Station.Application.Search;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class ArtistBrowseCacheTests
{
    [Fact]
    public async Task Concurrent_requests_share_load_and_invalidation_reloads()
    {
        var cache = new ArtistBrowseCache();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IReadOnlyList<ArtistBrowseItem>> Load(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(token);
            return [new(Guid.NewGuid(), "Test", 2)];
        }
        var requests = Enumerable.Range(0, 20).Select(_ => cache.GetAsync("db", Load, default)).ToArray();
        release.SetResult();
        await Task.WhenAll(requests);
        Assert.Equal(1, calls);
        Assert.All(requests, task => Assert.Same(requests[0].Result, task.Result));
        cache.Invalidate("db");
        await cache.GetAsync("db", Load, default);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Expiry_and_failed_load_do_not_leave_stale_cache()
    {
        var clock = new Clock();
        var cache = new ArtistBrowseCache(clock);
        var calls = 0;
        Task<IReadOnlyList<ArtistBrowseItem>> Load(CancellationToken _) { calls++; return Task.FromResult<IReadOnlyList<ArtistBrowseItem>>([]); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("db", _ => throw new InvalidOperationException(), default));
        await cache.GetAsync("db", Load, default);
        clock.Now += TimeSpan.FromSeconds(29);
        await cache.GetAsync("db", Load, default);
        Assert.Equal(1, calls);
        clock.Now += TimeSpan.FromSeconds(1);
        await cache.GetAsync("db", Load, default);
        Assert.Equal(2, calls);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
