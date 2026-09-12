using Station.Application.Common;
using Station.Application.Playback;
using Station.Desktop.ViewModels;
using Station.Domain.Models;

namespace Station.Desktop.Tests;

public sealed class PlaybackConsoleViewModelTests
{
    [Fact]
    public async Task Refresh_projects_player_state_and_separates_track_types()
    {
        var adapter = new FakePlayer(Snapshot());
        var viewModel = new PlaybackConsoleViewModel(new PlaybackControlService(adapter));

        await viewModel.RefreshAsync();

        Assert.Equal(PlayerLifecycleState.Playing, viewModel.State);
        Assert.Equal("00:03 / 00:10", viewModel.PositionText);
        Assert.Single(viewModel.AudioTracks);
        Assert.Single(viewModel.SubtitleTracks);
        Assert.Equal("伴奏", viewModel.AudioTracks[0].DisplayName);
        Assert.Equal("正在播放", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Volume_slider_applies_after_a_short_debounce_without_a_second_button()
    {
        var adapter = new FakePlayer(Snapshot());
        var viewModel = new PlaybackConsoleViewModel(new PlaybackControlService(adapter));
        await viewModel.RefreshAsync();

        viewModel.Volume = 42;
        await adapter.VolumeApplied.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(42, adapter.State.Volume);
    }

    [Fact]
    public async Task Controls_flow_through_application_service_and_refresh_snapshot()
    {
        var adapter = new FakePlayer(Snapshot());
        var viewModel = new PlaybackConsoleViewModel(new PlaybackControlService(adapter)) { Volume = 55 };

        await viewModel.RefreshAsync();
        viewModel.Volume = 55;
        viewModel.ApplyVolumeCommand.Execute(null);
        await adapter.VolumeApplied.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(55, adapter.State.Volume);
    }

    [Fact]
    public async Task Local_progress_interpolates_between_authoritative_refreshes()
    {
        var viewModel = new PlaybackConsoleViewModel(new PlaybackControlService(new FakePlayer(Snapshot())));
        await viewModel.RefreshAsync();
        var before = viewModel.PositionSeconds;

        await Task.Delay(120);
        viewModel.AdvanceLocalProgress();

        Assert.InRange(viewModel.PositionSeconds - before, 0.05, 0.5);
        Assert.Contains("/ 00:10", viewModel.PositionText);
    }

    private static PlayerSnapshot Snapshot() => new(PlayerLifecycleState.Playing, Guid.NewGuid(), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), 80, 1, 2,
        [new PlayerTrack(1, MediaTrackType.Audio, "aac", null, "伴奏", true), new PlayerTrack(2, MediaTrackType.Subtitle, "subrip", "zho", "歌词", true)]);

    private sealed class FakePlayer(PlayerSnapshot snapshot) : IPlayerAdapter
    {
        public PlayerSnapshot State { get; private set; } = snapshot;
        public TaskCompletionSource VolumeApplied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task<Result<PlayerSnapshot>> Ok() => Task.FromResult(Result<PlayerSnapshot>.Success(State));
        public Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default) { State = State with { Volume = volume }; VolumeApplied.TrySetResult(); return Ok(); }
        public Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default) => Ok();
        public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) { State = State with { State = PlayerLifecycleState.Playing }; return Ok(); }
        public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) { State = State with { State = PlayerLifecycleState.Paused }; return Ok(); }
        public Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) { State = State with { Position = position }; return Ok(); }
        public Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default) { State = State with { AudioTrackId = streamId }; return Ok(); }
        public Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default) { State = State with { SubtitleTrackId = streamId }; return Ok(); }
        public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
