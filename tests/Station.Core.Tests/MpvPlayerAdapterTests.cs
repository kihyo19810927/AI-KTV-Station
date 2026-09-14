using System.Collections.Concurrent;
using System.Diagnostics;
using Station.Application.Configuration;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class MpvPlayerAdapterTests
{
    [Fact]
    public async Task Missing_executable_is_classified_without_starting_a_process()
    {
        var options = new PlayerOptions
        {
            ExecutablePath = Path.Combine(Path.GetTempPath(), $"missing-mpv-{Guid.NewGuid():N}.exe"),
            CommandTimeoutSeconds = 1,
        };
        await using var player = new MpvPlayerAdapter(options);
        var result = await player.StartAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal("player.executable_missing", result.Error.Code);
    }

    [Fact]
    [Trait("Category", "External")]
    public async Task Generated_unicode_mkv_supports_state_tracks_switching_and_end_event()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_MPV");
        var media = Environment.GetEnvironmentVariable("KTV_STATION_MEDIA_FIXTURE");
        Assert.False(string.IsNullOrWhiteSpace(executable));
        Assert.False(string.IsNullOrWhiteSpace(media));
        await using var player = new MpvPlayerAdapter(new PlayerOptions
        {
            ExecutablePath = executable!,
            CommandTimeoutSeconds = 10,
        });

        var start = await player.StartAsync();
        Assert.True(start.IsSuccess, start.Error.Code);
        Assert.Equal(PlayerLifecycleState.Idle, start.Value.State);
        var processId = Assert.IsType<int>(player.ProcessId);

        var playbackId = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var endedTask = WaitForEndedAsync(player, playbackId, timeout.Token);
        var load = await player.LoadAsync(new PlayerLoadRequest(playbackId, media!), timeout.Token);
        Assert.True(load.IsSuccess, load.Error.Code);
        Assert.Equal(PlayerLifecycleState.Playing, load.Value.State);
        Assert.InRange(load.Value.Duration!.Value.TotalSeconds, 9.5, 10.5);
        var audio = load.Value.Tracks.Where(x => x.Type == MediaTrackType.Audio).ToArray();
        Assert.Equal(2, audio.Length);
        Assert.Contains(audio, x => x.Title == "伴奏");
        Assert.Contains(audio, x => x.Title == "原唱");
        var subtitle = Assert.Single(load.Value.Tracks, x => x.Type == MediaTrackType.Subtitle);

        foreach (var track in audio)
        {
            var selected = await player.SelectAudioTrackAsync(track.StreamId, timeout.Token);
            Assert.True(selected.IsSuccess, selected.Error.Code);
            Assert.Equal(track.StreamId, selected.Value.AudioTrackId);
        }
        var subtitleResult = await player.SelectSubtitleTrackAsync(subtitle.StreamId, timeout.Token);
        Assert.True(subtitleResult.IsSuccess, subtitleResult.Error.Code);
        Assert.Equal(subtitle.StreamId, subtitleResult.Value.SubtitleTrackId);

        var ended = await endedTask;
        Assert.Equal(PlaybackEndReason.Completed, ended.Reason);
        Assert.Equal(playbackId, ended.PlaybackId);
        Assert.Equal(processId, player.ProcessId);
        Assert.False(Process.GetProcessById(processId).HasExited);

        var skippedPlaybackId = Guid.NewGuid();
        var skippedTask = WaitForEndedAsync(player, skippedPlaybackId, timeout.Token);
        Assert.True((await player.LoadAsync(new PlayerLoadRequest(skippedPlaybackId, media!), timeout.Token)).IsSuccess);
        await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        var afterReplacement = await player.GetStateAsync(timeout.Token);
        Assert.True(afterReplacement.IsSuccess, afterReplacement.Error.Code);
        Assert.Equal(PlayerLifecycleState.Playing, afterReplacement.Value.State);
        Assert.True((await player.SkipAsync(timeout.Token)).IsSuccess);
        var skipped = await skippedTask;
        Assert.Equal(PlaybackEndReason.Stopped, skipped.Reason);
        Assert.True(skipped.IsUserInitiated);
        Assert.Equal(processId, player.ProcessId);
        Assert.False(Process.GetProcessById(processId).HasExited);

        var stopped = await player.StopAsync(timeout.Token);
        Assert.True(stopped.IsSuccess, stopped.Error.Code);
        Assert.Equal(PlayerLifecycleState.Stopped, stopped.Value.State);
    }

    [Fact]
    [Trait("Category", "External")]
    public async Task Unexpected_owned_mpv_exit_is_reported_as_retryable_failure()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_MPV");
        Assert.False(string.IsNullOrWhiteSpace(executable));
        await using var player = new MpvPlayerAdapter(new PlayerOptions
        {
            ExecutablePath = executable!,
            CommandTimeoutSeconds = 10,
        });
        var start = await player.StartAsync();
        Assert.True(start.IsSuccess, start.Error.Code);
        var processId = Assert.IsType<int>(player.ProcessId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var failureTask = WaitForFailureAsync(player, timeout.Token);

        using (var ownedProcess = Process.GetProcessById(processId))
        {
            ownedProcess.Kill(entireProcessTree: true);
            await ownedProcess.WaitForExitAsync(timeout.Token);
        }

        var failure = await failureTask;
        Assert.Equal(PlayerFailureKind.ProcessExited, failure.Failure.Kind);
        Assert.True(failure.Failure.IsRetryable);
        var state = await player.GetStateAsync(timeout.Token);
        Assert.True(state.IsSuccess);
        Assert.Equal(PlayerLifecycleState.Failed, state.Value.State);
    }

    [Fact]
    [Trait("Category", "External")]
    public async Task Loading_after_an_unexpected_exit_restarts_mpv_and_keeps_the_adapter_usable()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_MPV");
        var media = Environment.GetEnvironmentVariable("KTV_STATION_MEDIA_FIXTURE");
        Assert.False(string.IsNullOrWhiteSpace(executable));
        Assert.False(string.IsNullOrWhiteSpace(media));
        await using var player = new MpvPlayerAdapter(new PlayerOptions { ExecutablePath = executable!, CommandTimeoutSeconds = 10 });
        Assert.True((await player.StartAsync()).IsSuccess);
        var firstProcessId = Assert.IsType<int>(player.ProcessId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var failureTask = WaitForFailureAsync(player, timeout.Token);
        using (var ownedProcess = Process.GetProcessById(firstProcessId))
        {
            ownedProcess.Kill(entireProcessTree: true);
            await ownedProcess.WaitForExitAsync(timeout.Token);
        }
        await failureTask;

        var loaded = await player.LoadAsync(new PlayerLoadRequest(Guid.NewGuid(), media!), timeout.Token);
        Assert.True(loaded.IsSuccess, loaded.Error.Code);
        Assert.NotEqual(firstProcessId, player.ProcessId);
        Assert.Equal(PlayerLifecycleState.Playing, loaded.Value.State);
        Assert.True((await player.StopAsync(timeout.Token)).IsSuccess);
    }

    [Fact]
    [Trait("Category", "External")]
    public async Task Replacing_media_while_playing_does_not_end_the_new_playback()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_MPV");
        var media = Environment.GetEnvironmentVariable("KTV_STATION_MEDIA_FIXTURE");
        Assert.False(string.IsNullOrWhiteSpace(executable));
        Assert.False(string.IsNullOrWhiteSpace(media));
        await using var player = new MpvPlayerAdapter(new PlayerOptions { ExecutablePath = executable!, CommandTimeoutSeconds = 10 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var eventsCancellation = new CancellationTokenSource();
        var events = new ConcurrentBag<PlayerEvent>();
        var eventPump = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in player.WatchEventsAsync(eventsCancellation.Token)) events.Add(item);
            }
            catch (OperationCanceledException) when (eventsCancellation.IsCancellationRequested) { }
        });

        Assert.True((await player.StartAsync(timeout.Token)).IsSuccess);
        var playbackIds = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            var playbackId = Guid.NewGuid();
            playbackIds.Add(playbackId);
            var loaded = await player.LoadAsync(new PlayerLoadRequest(playbackId, media!), timeout.Token);
            Assert.True(loaded.IsSuccess, loaded.Error.Code);
            Assert.Equal(PlayerLifecycleState.Playing, loaded.Value.State);
            await Task.Delay(100, timeout.Token);
        }

        Assert.DoesNotContain(events, item => item is PlaybackEndedEvent ended &&
            playbackIds.Contains(ended.PlaybackId!.Value) &&
            ended.Reason is PlaybackEndReason.Completed or PlaybackEndReason.Stopped);
        Assert.Equal(PlayerLifecycleState.Playing, (await player.GetStateAsync(timeout.Token)).Value.State);

        Assert.True((await player.StopAsync(timeout.Token)).IsSuccess);
        eventsCancellation.Cancel();
        await eventPump;
    }

    private static async Task<PlaybackEndedEvent> WaitForEndedAsync(IPlayerAdapter player, Guid playbackId, CancellationToken cancellationToken)
    {
        await foreach (var item in player.WatchEventsAsync(cancellationToken))
            if (item is PlaybackEndedEvent ended && ended.PlaybackId == playbackId) return ended;
        throw new InvalidOperationException("Player event stream ended before playback completion.");
    }

    private static async Task<PlaybackFailedEvent> WaitForFailureAsync(IPlayerAdapter player, CancellationToken cancellationToken)
    {
        await foreach (var item in player.WatchEventsAsync(cancellationToken))
            if (item is PlaybackFailedEvent failure) return failure;
        throw new InvalidOperationException("Player event stream ended before a process failure was reported.");
    }
}
