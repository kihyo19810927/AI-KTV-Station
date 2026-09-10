using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Station.Application.Common;
using Station.Application.Playback;
using Station.Server.Hosting;

namespace Station.Core.Tests;

public sealed class StationEmbeddedHostTests
{
    [Fact]
    public async Task Shared_host_starts_on_configured_loopback_port_and_uses_supplied_player()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-embedded-{Guid.NewGuid():N}");
        var port = FreePort();
        var player = new SharedPlayer();
        var app = StationServerHost.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Station:Server:BindAddress"] = "127.0.0.1",
            ["Station:Server:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Station:Storage:DataDirectory"] = root,
        }), player);
        try
        {
            await StationServerHost.InitializeAsync(app.Services);
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Same(player, app.Services.GetRequiredService<IPlayerAdapter>());
            Assert.Contains(app.Services.GetServices<IHostedService>(), service => service.GetType().Name == "RoomPlaybackHostedService");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private sealed class SharedPlayer : IPlayerAdapter
    {
        private readonly PlayerSnapshot state = new(PlayerLifecycleState.Idle, null, TimeSpan.Zero, null, 70, null, null, []);
        private Task<Result<PlayerSnapshot>> Ok() => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) => Ok();
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
