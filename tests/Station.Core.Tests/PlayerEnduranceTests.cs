using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Station.Application.Configuration;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class PlayerEnduranceTests
{
    [Fact]
    [Trait("Category", "External")]
    public async Task Repeated_generated_media_completes_once_per_playback_without_restarting_mpv()
    {
        var executable = Environment.GetEnvironmentVariable("KTV_STATION_MPV");
        var media = Environment.GetEnvironmentVariable("KTV_STATION_MEDIA_FIXTURE");
        Assert.False(string.IsNullOrWhiteSpace(executable));
        Assert.False(string.IsNullOrWhiteSpace(media));
        var cycles = int.TryParse(Environment.GetEnvironmentVariable("KTV_STATION_ENDURANCE_CYCLES"), out var configured) ? configured : 20;
        Assert.InRange(cycles, 1, 100);
        await using var player = new MpvPlayerAdapter(new PlayerOptions { ExecutablePath = executable!, CommandTimeoutSeconds = 10 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, cycles * 3)));
        var started = await player.StartAsync(timeout.Token);
        Assert.True(started.IsSuccess, started.Error.Code);
        var processId = Assert.IsType<int>(player.ProcessId);
        using var ownedProcess = Process.GetProcessById(processId);
        ownedProcess.Refresh();
        var initialWorkingSet = ownedProcess.WorkingSet64;
        var completed = Channel.CreateUnbounded<PlaybackEndedEvent>();
        var counts = new ConcurrentDictionary<Guid, int>();
        var eventPump = PumpEventsAsync(player, completed.Writer, counts, timeout.Token);
        var stopwatch = Stopwatch.StartNew();

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var playbackId = Guid.NewGuid();
            var loaded = await player.LoadAsync(new PlayerLoadRequest(playbackId, media!), timeout.Token);
            Assert.True(loaded.IsSuccess, $"cycle={cycle}; error={loaded.Error.Code}");
            var audioTracks = loaded.Value.Tracks.Where(x => x.Type == MediaTrackType.Audio).OrderBy(x => x.StreamId).ToArray();
            Assert.Equal(2, audioTracks.Length);
            var audio = await player.SelectAudioTrackAsync(audioTracks[cycle % audioTracks.Length].StreamId, timeout.Token);
            Assert.True(audio.IsSuccess, $"cycle={cycle}; error={audio.Error.Code}");
            var subtitle = loaded.Value.Tracks.Single(x => x.Type == MediaTrackType.Subtitle);
            var subtitleResult = await player.SelectSubtitleTrackAsync(cycle % 2 == 0 ? subtitle.StreamId : null, timeout.Token);
            Assert.True(subtitleResult.IsSuccess, $"cycle={cycle}; error={subtitleResult.Error.Code}");
            var seekPosition = loaded.Value.Duration!.Value - TimeSpan.FromMilliseconds(400);
            var seek = await player.SeekAsync(seekPosition, timeout.Token);
            Assert.True(seek.IsSuccess, $"cycle={cycle}; error={seek.Error.Code}");

            PlaybackEndedEvent ended;
            do { ended = await completed.Reader.ReadAsync(timeout.Token); }
            while (ended.PlaybackId != playbackId);
            Assert.Equal(PlaybackEndReason.Completed, ended.Reason);
            Assert.Equal(PlayerLifecycleState.Ended, (await player.GetStateAsync(timeout.Token)).Value.State);
            Assert.Equal(processId, player.ProcessId);
        }

        ownedProcess.Refresh();
        var finalWorkingSet = ownedProcess.WorkingSet64;
        await player.StopAsync(timeout.Token);
        await Task.Delay(100, timeout.Token);
        timeout.Cancel();
        try { await eventPump; } catch (OperationCanceledException) { }
        stopwatch.Stop();
        Assert.Equal(cycles, counts.Count);
        Assert.All(counts.Values, count => Assert.Equal(1, count));
        var reportPath = Environment.GetEnvironmentVariable("KTV_STATION_ENDURANCE_REPORT");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            var report = new
            {
                cycles,
                completedPlaybacks = counts.Count,
                duplicateEndEvents = counts.Values.Count(x => x != 1),
                elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 3),
                processRestarts = 0,
                initialWorkingSetBytes = initialWorkingSet,
                finalWorkingSetBytes = finalWorkingSet,
                generatedFixture = true,
                completedAt = DateTimeOffset.UtcNow,
            };
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static async Task PumpEventsAsync(
        IPlayerAdapter player,
        ChannelWriter<PlaybackEndedEvent> output,
        ConcurrentDictionary<Guid, int> counts,
        CancellationToken cancellationToken)
    {
        await foreach (var item in player.WatchEventsAsync(cancellationToken))
        {
            if (item is not PlaybackEndedEvent ended || ended.PlaybackId is not { } playbackId) continue;
            counts.AddOrUpdate(playbackId, 1, (_, count) => count + 1);
            await output.WriteAsync(ended, cancellationToken);
        }
    }
}
