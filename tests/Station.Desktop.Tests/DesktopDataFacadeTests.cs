using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Station.Application.Search;
using Station.Desktop.Services;
using Station.Infrastructure.Persistence;

namespace Station.Desktop.Tests;

public sealed class DesktopDataFacadeTests
{
    [Fact]
    public async Task Concurrent_reads_have_independent_untracked_scopes_and_dispose_services()
    {
        var observations = new ConcurrentBag<Guid>();
        var disposed = 0;
        var services = new ServiceCollection();
        services.AddDbContext<StationDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddScoped<IArtistBrowseService>(provider => new Reader(
            provider.GetRequiredService<StationDbContext>(), observations, () => Interlocked.Increment(ref disposed)));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var facade = new DesktopDataFacade(() => provider);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => facade.ListAsync((string?)null)));
        Assert.Equal(20, observations.Distinct().Count());
        Assert.Equal(20, disposed);
    }

    private sealed class Reader(StationDbContext database, ConcurrentBag<Guid> observations, Action disposed)
        : IArtistBrowseService, IDisposable
    {
        public async Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default)
        {
            Assert.True(Thread.CurrentThread.IsThreadPoolThread);
            Assert.Equal(QueryTrackingBehavior.NoTracking, database.ChangeTracker.QueryTrackingBehavior);
            observations.Add(database.ContextId.InstanceId);
            await Task.Yield();
            return [];
        }
        public void Dispose() => disposed();
    }
}
