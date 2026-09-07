using Microsoft.Extensions.DependencyInjection;
using Station.Application.Common;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Server.Scanning;

namespace Station.Core.Tests;

public sealed class ScanCoordinatorTests
{
    [Fact]
    public async Task Runs_in_its_own_scope_reports_progress_rebuilds_search_and_rejects_duplicate_source()
    {
        var runner = new ControlledRunner();
        var index = new RecordingSearchIndex();
        await using var provider = CreateProvider(runner, index);
        var coordinator = new ScanCoordinator(provider.GetRequiredService<IServiceScopeFactory>());
        var sourceId = Guid.NewGuid();

        var started = coordinator.Start(sourceId);
        Assert.True(started.IsSuccess);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("scan.already_running", coordinator.Start(sourceId).Error.Code);
        var running = coordinator.Get(started.Value.ScanRunId).Value;
        Assert.Equal(3, running.DiscoveredFiles);
        Assert.Equal(1, running.UpdatedFiles);

        runner.Complete();
        var completed = await WaitForTerminalAsync(coordinator, started.Value.ScanRunId);
        Assert.Equal(ScanStatus.Completed, completed.Status);
        Assert.True(index.WasRebuilt);
    }

    [Fact]
    public async Task Cancellation_is_scoped_to_the_requested_operation()
    {
        var runner = new ControlledRunner();
        await using var provider = CreateProvider(runner, new RecordingSearchIndex());
        var coordinator = new ScanCoordinator(provider.GetRequiredService<IServiceScopeFactory>());
        var started = coordinator.Start(Guid.NewGuid()).Value;
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(coordinator.Cancel(started.ScanRunId).IsSuccess);

        var completed = await WaitForTerminalAsync(coordinator, started.ScanRunId);
        Assert.Equal(ScanStatus.Cancelled, completed.Status);
        Assert.Equal("scan.operation_finished", coordinator.Cancel(started.ScanRunId).Error.Code);
    }

    private static ServiceProvider CreateProvider(IMediaScanRunner runner, ISongSearchIndex index) => new ServiceCollection()
        .AddScoped<IMediaScanRunner>(_ => runner)
        .AddScoped<ISongSearchIndex>(_ => index)
        .BuildServiceProvider();

    private static async Task<ScanOperationStatus> WaitForTerminalAsync(IScanCoordinator coordinator, Guid id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var status = coordinator.Get(id).Value;
            if (status.Status is not (ScanStatus.Pending or ScanStatus.Running)) return status;
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ControlledRunner : IMediaScanRunner
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete() => completion.TrySetResult();

        public async Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, Guid scanRunId, IProgress<MediaScanProgress> progress, CancellationToken cancellationToken = default)
        {
            progress.Report(new MediaScanProgress(scanRunId, ScanStatus.Running, 3, 1, 0));
            Started.TrySetResult();
            await completion.Task.WaitAsync(cancellationToken);
            return Result<ScanRun>.Success(new ScanRun
            {
                Id = scanRunId,
                MediaSourceId = mediaSourceId,
                Status = ScanStatus.Completed,
                DiscoveredFiles = 3,
                UpdatedFiles = 1,
            });
        }
    }

    private sealed class RecordingSearchIndex : ISongSearchIndex
    {
        public bool WasRebuilt { get; private set; }
        public Task RebuildAsync(CancellationToken cancellationToken = default) { WasRebuilt = true; return Task.CompletedTask; }
        public Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<SongSearchPage>.Success(new SongSearchPage([], 0, query.Page, query.PageSize)));
    }
}
