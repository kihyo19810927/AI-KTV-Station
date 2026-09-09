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
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Server.Api;

namespace Station.Core.Tests;

public sealed class StationApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Room_join_search_queue_and_host_permissions_form_one_path_safe_contract()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/rooms", new { maxQueuedSongsPerGuest = 3, hostNickname = "主控" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<RoomCreatedResponse>(JsonOptions);
        Assert.NotNull(created);

        var joinResponse = await client.PostAsJsonAsync("/api/rooms/join", new { joinCode = created.Room.JoinCode, nickname = "小满" });
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        var guest = await joinResponse.Content.ReadFromJsonAsync<IssuedRoomToken>(JsonOptions);
        Assert.NotNull(guest);

        var songs = await SeedCatalogAsync(factory);
        SetBearer(client, guest.Token);
        var search = await client.GetFromJsonAsync<SongSearchPage>("/api/catalog/search?text=yequ", JsonOptions);
        Assert.NotNull(search);
        Assert.Equal(songs[0], Assert.Single(search.Items).SongId);

        var firstResponse = await client.PostAsJsonAsync("/api/queue", new { songId = songs[0] });
        var secondResponse = await client.PostAsJsonAsync("/api/queue", new { songId = songs[1] });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<QueueEntry>(JsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<QueueEntry>(JsonOptions);
        Assert.NotNull(first);
        Assert.NotNull(second);

        var queueJson = await client.GetStringAsync("/api/queue");
        Assert.DoesNotContain("RelativePath", queueJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RootPath", queueJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/queue/{second.Id}/top", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/playback/pause", null)).StatusCode);

        SetBearer(client, created.Host.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/queue/{second.Id}/top", null)).StatusCode);
        var ordered = await client.GetFromJsonAsync<QueueEntry[]>("/api/queue", JsonOptions);
        Assert.Equal(second.Id, ordered![0].Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/queue/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/playback")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/playback/volume", new { volume = 65 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/rooms/{created.Room.Id}/close", null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/queue")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_lists_v1_room_queue_catalog_and_playback_operations()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/rooms/join", document);
        Assert.Contains("/api/catalog/search", document);
        Assert.Contains("/api/queue", document);
        Assert.Contains("/api/playback/volume", document);
        Assert.DoesNotContain("RootPath", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RelativePath", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TokenHash", document, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<Guid[]> SeedCatalogAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
        var songs = new[]
        {
            Song("夜曲", "yequ"),
            Song("后来", "houlai"),
        };
        database.Songs.AddRange(songs);
        await database.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ISongSearchIndex>().UpsertAsync(songs.Select(x => x.Id).ToArray());
        return songs.Select(x => x.Id).ToArray();
    }

    private static Song Song(string title, string pinyin) => new()
    {
        Title = title,
        NormalizedTitle = title,
        SimplifiedTitle = title,
        TraditionalTitle = title,
        TitlePinyin = pinyin,
        TitleInitials = string.Concat(pinyin.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x[0])),
        CompactTitle = title,
        Availability = AvailabilityStatus.Available,
    };

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-ktv-full-api-{Guid.NewGuid():N}");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Station:Storage:DataDirectory", dataDirectory);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlayerAdapter>();
                services.AddSingleton<IPlayerAdapter, ApiPlayer>();
            });
        }
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, true);
        }
    }

    private sealed class ApiPlayer : IPlayerAdapter
    {
        private PlayerSnapshot snapshot = new(PlayerLifecycleState.Playing, Guid.NewGuid(), TimeSpan.FromSeconds(12), TimeSpan.FromMinutes(4), 70, 1, 3,
            [new PlayerTrack(1, MediaTrackType.Audio, "aac", "zho", "伴奏", true), new PlayerTrack(2, MediaTrackType.Audio, "aac", "zho", "原唱", false), new PlayerTrack(3, MediaTrackType.Subtitle, "ass", "zho", "中文", true)]);
        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) { snapshot = snapshot with { Volume = volume }; return Success(); }
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default) => Success();
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Playing }; return Success(); }
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) { snapshot = snapshot with { State = PlayerLifecycleState.Paused }; return Success(); }
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) { snapshot = snapshot with { Position = position }; return Success(); }
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) { snapshot = snapshot with { AudioTrackId = streamId }; return Success(); }
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default) { snapshot = snapshot with { SubtitleTrackId = streamId }; return Success(); }
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private Task<Result<PlayerSnapshot>> Success() => Task.FromResult(Result<PlayerSnapshot>.Success(snapshot));
    }
}
