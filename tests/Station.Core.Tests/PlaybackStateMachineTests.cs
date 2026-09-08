using Station.Application.Playback;

namespace Station.Core.Tests;

public sealed class PlaybackStateMachineTests
{
    [Fact]
    public void Applies_legal_start_load_play_pause_resume_and_end_sequence()
    {
        var machine = new PlaybackStateMachine();
        var time = DateTimeOffset.UtcNow;
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(new PlayerStartedEvent(Guid.NewGuid(), time)).Disposition);
        var playbackId = Guid.NewGuid();
        Assert.True(machine.BeginLoad(playbackId).IsSuccess);
        Assert.Equal(PlayerLifecycleState.Preparing, machine.Current.State);
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(State(playbackId, PlayerLifecycleState.Preparing, PlayerLifecycleState.Playing, time.AddSeconds(1))).Disposition);
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(State(playbackId, PlayerLifecycleState.Playing, PlayerLifecycleState.Paused, time.AddSeconds(2))).Disposition);
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(State(playbackId, PlayerLifecycleState.Paused, PlayerLifecycleState.Playing, time.AddSeconds(3))).Disposition);
        var ended = new PlaybackEndedEvent(Guid.NewGuid(), time.AddSeconds(4), playbackId, PlaybackEndReason.Completed);
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(ended).Disposition);
        Assert.Equal(PlayerLifecycleState.Ended, machine.Current.State);
    }

    [Fact]
    public void Rejects_illegal_transition_without_mutating_state()
    {
        var machine = new PlaybackStateMachine();
        var transition = machine.Apply(new PlaybackStartedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()));
        Assert.Equal(PlayerEventDisposition.Rejected, transition.Disposition);
        Assert.Equal("player.invalid_transition", transition.Error.Code);
        Assert.Equal(PlayerLifecycleState.Stopped, machine.Current.State);
        Assert.Equal(0, machine.Current.Revision);
    }

    [Fact]
    public void Ignores_event_from_replaced_playback_instance()
    {
        var machine = StartedMachine();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Assert.True(machine.BeginLoad(first).IsSuccess);
        Assert.True(machine.BeginLoad(second).IsSuccess);
        var revision = machine.Current.Revision;
        var stale = new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, first, PlaybackEndReason.Replaced);
        Assert.Equal(PlayerEventDisposition.Stale, machine.Apply(stale).Disposition);
        Assert.Equal(second, machine.Current.PlaybackId);
        Assert.Equal(revision, machine.Current.Revision);
    }

    [Fact]
    public void Duplicate_event_id_is_idempotent()
    {
        var machine = StartedMachine();
        var playbackId = Guid.NewGuid();
        Assert.True(machine.BeginLoad(playbackId).IsSuccess);
        var item = State(playbackId, PlayerLifecycleState.Preparing, PlayerLifecycleState.Playing, DateTimeOffset.UtcNow);
        Assert.Equal(PlayerEventDisposition.Applied, machine.Apply(item).Disposition);
        var revision = machine.Current.Revision;
        Assert.Equal(PlayerEventDisposition.Duplicate, machine.Apply(item).Disposition);
        Assert.Equal(revision, machine.Current.Revision);
    }

    [Fact]
    public void Older_event_timestamp_cannot_regress_current_playback()
    {
        var machine = StartedMachine();
        var playbackId = Guid.NewGuid();
        Assert.True(machine.BeginLoad(playbackId).IsSuccess);
        var newer = DateTimeOffset.UtcNow;
        machine.Apply(State(playbackId, PlayerLifecycleState.Preparing, PlayerLifecycleState.Playing, newer));
        var late = State(playbackId, PlayerLifecycleState.Playing, PlayerLifecycleState.Paused, newer.AddMilliseconds(-1));
        Assert.Equal(PlayerEventDisposition.Stale, machine.Apply(late).Disposition);
        Assert.Equal(PlayerLifecycleState.Playing, machine.Current.State);
    }

    [Fact]
    public void Failure_retains_public_retry_classification()
    {
        var machine = StartedMachine();
        var playbackId = Guid.NewGuid();
        Assert.True(machine.BeginLoad(playbackId).IsSuccess);
        var failure = new PlayerFailure("player.process_exited", PlayerFailureKind.ProcessExited, true, "Player stopped unexpectedly.");
        var transition = machine.Apply(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, failure));
        Assert.Equal(PlayerEventDisposition.Applied, transition.Disposition);
        Assert.Equal(PlayerLifecycleState.Failed, transition.State.State);
        Assert.Same(failure, transition.State.Failure);
    }

    private static PlaybackStateMachine StartedMachine()
    {
        var machine = new PlaybackStateMachine();
        machine.Apply(new PlayerStartedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(-1)));
        return machine;
    }

    private static PlayerStateChangedEvent State(Guid playbackId, PlayerLifecycleState previous, PlayerLifecycleState current, DateTimeOffset time) =>
        new(Guid.NewGuid(), time, playbackId, previous, current);
}
