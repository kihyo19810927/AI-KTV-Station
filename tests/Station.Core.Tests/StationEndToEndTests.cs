using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Station.Application.Common;
using Station.Application.Media;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;
using Station.Server.Api;

namespace Station.Core.Tests;

public sealed class StationEndToEndTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Generated_catalog_file_flows_through_scan_search_request_playback_and_completion()
    {
        await using var factory = new EndToEndFactory();
        Directory.CreateDirectory(factory.MediaDirectory);
        var mediaPath = Path.Combine(factory.MediaDirectory, "周杰伦-夜曲-国语-流行.mpg");
        await File.WriteAllTextAsync(mediaPath, "deterministic test media placeholder");
        await File.WriteAllTextAsync(Path.ChangeExtension(mediaPath, ".ksc"), "karaoke lyrics fixture");

        using var client = factory.CreateClient();
        Guid sourceId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            var source = new MediaSource { Name = "端到端夹具", RootPath = factory.MediaDirectory, Availability = AvailabilityStatus.Available };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync();
            sourceId = source.Id;
        }

        var scanStart = await client.PostAsJsonAsync("/api/scans", new { mediaSourceId = sourceId });
        Assert.Equal(HttpStatusCode.Accepted, scanStart.StatusCode);
        var scan = await scanStart.Content.ReadFromJsonAsync<ScanOperationStatus>(JsonOptions);
        Assert.NotNull(scan);
        scan = await WaitForCompletedScanAsync(client, scan.ScanRunId);
        Assert.Equal(2, scan.DiscoveredFiles);

        var roomResponse = await client.PostAsJsonAsync("/api/rooms", new { maxQueuedSongsPerGuest = 3, hostNickname = "主控" });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomCreatedResponse>(JsonOptions);
        Assert.NotNull(room);
        var joinResponse = await client.PostAsJsonAsync("/api/rooms/join", new { joinCode = room.Room.JoinCode, nickname = "小满" });
        var guest = await joinResponse.Content.ReadFromJsonAsync<IssuedRoomToken>(JsonOptions);
        Assert.NotNull(guest);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest.Token);

        var search = await client.GetFromJsonAsync<SongSearchPage>("/api/catalog/search?text=yequ", JsonOptions);
        var song = Assert.Single(search!.Items);
        var queuedResponse = await client.PostAsJsonAsync("/api/queue", new { songId = song.SongId });
        Assert.Equal(HttpStatusCode.Created, queuedResponse.StatusCode);
        var queued = await queuedResponse.Content.ReadFromJsonAsync<QueueEntry>(JsonOptions);
        Assert.NotNull(queued);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            var orchestrator = new QueuePlaybackOrchestrator(
                factory.Player,
                new EfPlaybackQueueStore(database),
                new PlaybackRecoveryService(new EfPlaybackFailureStore(database), new PlaybackRecoveryPolicy(0, TimeSpan.Zero)),
                TimeProvider.System);
            var started = await orchestrator.StartAsync(room.Room.Id);
            Assert.True(started.IsSuccess);
            Assert.Equal(queued.Id, started.Value.QueueItemId);
            Assert.Equal(Path.GetFullPath(mediaPath), Path.GetFullPath(factory.Player.LoadedPath!));

            var completed = await orchestrator.HandleAsync(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, factory.Player.PlaybackId!.Value, PlaybackEndReason.Completed));
            Assert.True(completed.IsSuccess);
            Assert.Null(completed.Value.QueueItemId);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            Assert.Equal(QueueItemStatus.Completed, (await database.QueueItems.SingleAsync(x => x.Id == queued.Id)).Status);
            Assert.Equal(PlaybackOutcome.Completed, (await database.PlayHistory.SingleAsync()).Outcome);
            var indexedMedia = await database.MediaFiles.SingleAsync();
            Assert.EndsWith(".ksc", indexedMedia.LyricsRelativePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<ScanOperationStatus> WaitForCompletedScanAsync(HttpClient client, Guid scanRunId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var current = await client.GetFromJsonAsync<ScanOperationStatus>($"/api/scans/{scanRunId}/result", JsonOptions, timeout.Token);
            Assert.NotNull(current);
            if (current.Status is not (ScanStatus.Pending or ScanStatus.Running)) return current;
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class EndToEndFactory : WebApplicationFactory<Program>
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"ai-ktv-e2e-{Guid.NewGuid():N}");
        public string MediaDirectory => Path.Combine(root, "Unicode 曲库");
        public TestPlayer Player { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Station:Storage:DataDirectory", Path.Combine(root, "data"));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMediaProbe>();
                services.AddScoped<IMediaProbe, FixtureMediaProbe>();
                services.RemoveAll<IPlayerAdapter>();
                services.AddSingleton<IPlayerAdapter>(Player);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FixtureMediaProbe : IMediaProbe
    {
        public Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<MediaProbeResult>.Success(new(10,
            [
                new(0, MediaTrackType.Video, "mpeg2video", null, null),
                new(1, MediaTrackType.Audio, "mp2", "zho", "伴奏"),
                new(2, MediaTrackType.Audio, "mp2", "zho", "原唱"),
            ])));
    }

    public sealed class TestPlayer : IPlayerAdapter
    {
        private PlayerSnapshot snapshot = new(PlayerLifecycleState.Stopped, null, TimeSpan.Zero, null, 70, null, null, []);
        public string? LoadedPath { get; private set; }
        public Guid? PlaybackId => snapshot.PlaybackId;
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Idle }; return Success(); }
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Stopped }; return Success(); }
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default) { LoadedPath = request.MediaPath; snapshot = snapshot with { State = PlayerLifecycleState.Playing, PlaybackId = request.PlaybackId }; return Success(); }
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Playing }; return Success(); }
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Paused }; return Success(); }
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) { snapshot = snapshot with { Position = position }; return Success(); }
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) { snapshot = snapshot with { Volume = volume }; return Success(); }
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) => Success();
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private Task<Result<PlayerSnapshot>> Success() => Task.FromResult(Result<PlayerSnapshot>.Success(snapshot));
    }
}
