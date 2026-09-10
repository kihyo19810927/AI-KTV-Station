using Station.Application.Common;
using Station.Application.Playback;
using Station.Domain.Models;

namespace Station.Core.Tests;

public sealed class PlaybackControlServiceTests
{
    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public async Task Invalid_volume_is_rejected_without_calling_adapter(double volume)
    {
        var adapter = new RecordingPlayerAdapter(ActiveSnapshot());
        var result = await new PlaybackControlService(adapter).SetVolumeAsync(volume);
        Assert.Equal("player.invalid_volume", result.Error.Code);
        Assert.Equal(0, adapter.VolumeCalls);
    }

    [Fact]
    public async Task Progress_contains_no_tracks_or_media_path()
    {
        var snapshot = ActiveSnapshot() with { Position = TimeSpan.FromSeconds(4) };
        var result = await new PlaybackControlService(new RecordingPlayerAdapter(snapshot)).GetProgressAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(4), result.Value.Position);
        Assert.DoesNotContain(typeof(PlaybackProgress).GetProperties(), x =>
            x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Track", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Seek_requires_active_playback_and_known_duration_boundary()
    {
        var inactive = new RecordingPlayerAdapter(ActiveSnapshot() with { State = PlayerLifecycleState.Idle, PlaybackId = null });
        Assert.Equal("player.not_playing", (await new PlaybackControlService(inactive).SeekAsync(TimeSpan.Zero)).Error.Code);
        Assert.Equal(0, inactive.SeekCalls);

        var active = new RecordingPlayerAdapter(ActiveSnapshot());
        Assert.Equal("player.position_out_of_range", (await new PlaybackControlService(active).SeekAsync(TimeSpan.FromSeconds(11))).Error.Code);
        Assert.Equal(0, active.SeekCalls);
    }

    [Fact]
    public async Task Valid_seek_returns_adapter_synchronized_snapshot()
    {
        var adapter = new RecordingPlayerAdapter(ActiveSnapshot());
        var result = await new PlaybackControlService(adapter).SeekAsync(TimeSpan.FromSeconds(3));
        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Value.Position);
        Assert.Equal(1, adapter.SeekCalls);
    }

    [Fact]
    public async Task Skip_requires_active_playback_and_stops_the_adapter()
    {
        var adapter = new RecordingPlayerAdapter(ActiveSnapshot());
        Assert.True((await new PlaybackControlService(adapter).SkipAsync()).IsSuccess);
        Assert.Equal(1, adapter.StopCalls);
    }

    [Fact]
    public async Task Subtitle_must_reference_a_subtitle_track_and_can_be_disabled()
    {
        var adapter = new RecordingPlayerAdapter(ActiveSnapshot());
        var service = new PlaybackControlService(adapter);
        Assert.Equal("player.subtitle_track_not_found", (await service.SelectSubtitleAsync(1)).Error.Code);
        Assert.Equal(0, adapter.SubtitleCalls);
        Assert.True((await service.SelectSubtitleAsync(2)).IsSuccess);
        var disabled = await service.SelectSubtitleAsync(null);
        Assert.True(disabled.IsSuccess);
        Assert.Null(disabled.Value.SubtitleTrackId);
        Assert.Equal(2, adapter.SubtitleCalls);
    }

    [Fact]
    public async Task Adapter_error_is_preserved_without_protocol_detail_translation()
    {
        var adapter = new RecordingPlayerAdapter(ActiveSnapshot())
        {
            StateError = new Error("player.command_timeout", "The player command timed out."),
        };
        var result = await new PlaybackControlService(adapter).GetProgressAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal("player.command_timeout", result.Error.Code);
    }

    private static PlayerSnapshot ActiveSnapshot() => new(
        PlayerLifecycleState.Playing,
        Guid.NewGuid(),
        TimeSpan.Zero,
        TimeSpan.FromSeconds(10),
        80,
        1,
        2,
        [
            new PlayerTrack(1, MediaTrackType.Audio, "aac", null, "伴奏", true),
            new PlayerTrack(2, MediaTrackType.Subtitle, "subrip", "zho", "测试字幕", true),
        ]);

    private sealed class RecordingPlayerAdapter(PlayerSnapshot state) : IPlayerAdapter
    {
        private PlayerSnapshot state = state;
        public int VolumeCalls { get; private set; }
        public int SeekCalls { get; private set; }
        public int SubtitleCalls { get; private set; }
        public int StopCalls { get; private set; }
        public Error? StateError { get; init; }

        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(StateError is null ? Result<PlayerSnapshot>.Success(state) : Result<PlayerSnapshot>.Failure(StateError));
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            SeekCalls++;
            state = state with { Position = position };
            return Task.FromResult(Result<PlayerSnapshot>.Success(state));
        }
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
        {
            VolumeCalls++;
            state = state with { Volume = volume };
            return Task.FromResult(Result<PlayerSnapshot>.Success(state));
        }
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default)
        {
            SubtitleCalls++;
            state = state with { SubtitleTrackId = streamId };
            return Task.FromResult(Result<PlayerSnapshot>.Success(state));
        }
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) { StopCalls++; return Task.FromResult(Result<PlayerSnapshot>.Success(state)); }
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) => Task.FromResult(Result<PlayerSnapshot>.Success(state));
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
