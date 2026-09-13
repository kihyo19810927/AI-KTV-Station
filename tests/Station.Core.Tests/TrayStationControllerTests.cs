using Station.Application.Common;
using Station.Application.Playback;
using Station.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Station.Core.Tests;

public sealed class TrayStationControllerTests
{
    [Fact]
    public async Task Start_opens_desktop_once_and_exit_stops_only_the_registered_player()
    {
        var runtime = new FakeRuntime();
        var player = new FakePlayer();
        var edge = new FakeEdge();
        await using var controller = new TrayStationController(runtime, player, edge, new Uri("http://127.0.0.1:5090/desk"));

        Assert.True(await controller.StartAsync());
        Assert.True(await controller.StartAsync());
        await controller.StopAsync();

        Assert.Equal(1, runtime.Initialized);
        Assert.Equal(1, runtime.Started);
        Assert.Equal(1, runtime.Stopped);
        Assert.Equal(1, player.StopCalls);
        Assert.Equal(1, edge.OpenCalls);
    }

    [Fact]
    public async Task Browser_failure_does_not_stop_the_local_server()
    {
        var runtime = new FakeRuntime();
        var player = new FakePlayer();
        await using var controller = new TrayStationController(runtime, player, new FakeEdge { Result = false }, new Uri("http://127.0.0.1:5090/desk"));

        Assert.False(await controller.StartAsync());
        Assert.Equal(1, runtime.Started);
        await controller.StopAsync();
        Assert.Equal(1, runtime.Stopped);
    }

    private sealed class FakeRuntime : ITrayStationRuntime
    {
        public IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();
        public int Initialized { get; private set; }
        public int Started { get; private set; }
        public int Stopped { get; private set; }
        public Task InitializeAsync(CancellationToken cancellationToken = default) { Initialized++; return Task.CompletedTask; }
        public Task StartAsync(CancellationToken cancellationToken = default) { Started++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default) { Stopped++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeEdge : IEdgeAppLauncher
    {
        public bool Result { get; set; } = true;
        public int OpenCalls { get; private set; }
        public bool TryOpen(Uri address) { OpenCalls++; return Result; }
    }

    private sealed class FakePlayer : IPlayerAdapter
    {
        private readonly PlayerSnapshot snapshot = new(PlayerLifecycleState.Idle, null, TimeSpan.Zero, null, 70, null, null, []);
        public int StopCalls { get; private set; }
        private Task<Result<PlayerSnapshot>> Ok() => Task.FromResult(Result<PlayerSnapshot>.Success(snapshot));
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) { StopCalls++; return Ok(); }
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) => Ok();
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
