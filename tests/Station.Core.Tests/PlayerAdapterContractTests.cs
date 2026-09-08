using Station.Application.Playback;

namespace Station.Core.Tests;

public sealed class PlayerAdapterContractTests
{
    [Fact]
    public void Contract_exposes_required_commands_and_async_event_stream()
    {
        var methods = typeof(IPlayerAdapter).GetMethods().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            "StartAsync", "StopAsync", "LoadAsync", "PlayAsync", "PauseAsync", "SeekAsync", "SetVolumeAsync",
            "SelectAudioTrackAsync", "SelectSubtitleTrackAsync", "GetStateAsync", "WatchEventsAsync",
        };
        Assert.All(required, name => Assert.Contains(name, methods));
        Assert.Equal("Station.Application", typeof(IPlayerAdapter).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(IPlayerAdapter).Assembly.GetReferencedAssemblies(), x =>
            x.Name is not null && (x.Name.Contains("mpv", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(double.NaN)]
    public void Invalid_volume_has_stable_error(double value) =>
        Assert.Equal("player.invalid_volume", PlayerCommandValidation.ValidateVolume(value)?.Code);

    [Fact]
    public void Public_state_events_and_failures_do_not_have_path_or_raw_protocol_fields()
    {
        var contractTypes = new[] { typeof(PlayerSnapshot), typeof(PlayerTrack), typeof(PlayerFailure), typeof(PlayerEvent) };
        Assert.All(contractTypes, type => Assert.DoesNotContain(type.GetProperties(), property =>
            property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Json", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Ipc", StringComparison.OrdinalIgnoreCase)));
        var failure = new PlayerFailure("player.process_exited", PlayerFailureKind.ProcessExited, true, "Player stopped unexpectedly.");
        var playbackId = Guid.NewGuid();
        var ended = new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, PlaybackEndReason.Completed);
        Assert.True(failure.IsRetryable);
        Assert.Equal(playbackId, ended.PlaybackId);
        Assert.NotEqual(Guid.Empty, ended.EventId);
    }
}
