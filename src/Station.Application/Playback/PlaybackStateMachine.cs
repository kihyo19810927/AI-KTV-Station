using Station.Application.Common;

namespace Station.Application.Playback;

public enum PlayerEventDisposition { Applied, Duplicate, Stale, Rejected }

public sealed record PlaybackMachineState(
    PlayerLifecycleState State,
    Guid? PlaybackId,
    long Revision,
    DateTimeOffset? LastEventAt,
    PlayerFailure? Failure = null);

public sealed record PlayerEventTransition(
    PlayerEventDisposition Disposition,
    PlaybackMachineState State,
    Error Error);

public sealed class PlaybackStateMachine
{
    private const int RememberedEventLimit = 2048;
    private static readonly IReadOnlyDictionary<PlayerLifecycleState, PlayerLifecycleState[]> AllowedTransitions =
        new Dictionary<PlayerLifecycleState, PlayerLifecycleState[]>
        {
            [PlayerLifecycleState.Stopped] = [PlayerLifecycleState.Idle, PlayerLifecycleState.Failed],
            [PlayerLifecycleState.Idle] = [PlayerLifecycleState.Preparing, PlayerLifecycleState.Stopped, PlayerLifecycleState.Failed],
            [PlayerLifecycleState.Preparing] = [PlayerLifecycleState.Playing, PlayerLifecycleState.Paused, PlayerLifecycleState.Ended, PlayerLifecycleState.Failed, PlayerLifecycleState.Stopped],
            [PlayerLifecycleState.Playing] = [PlayerLifecycleState.Paused, PlayerLifecycleState.Ended, PlayerLifecycleState.Failed, PlayerLifecycleState.Stopped, PlayerLifecycleState.Preparing],
            [PlayerLifecycleState.Paused] = [PlayerLifecycleState.Playing, PlayerLifecycleState.Ended, PlayerLifecycleState.Failed, PlayerLifecycleState.Stopped, PlayerLifecycleState.Preparing],
            [PlayerLifecycleState.Ended] = [PlayerLifecycleState.Preparing, PlayerLifecycleState.Idle, PlayerLifecycleState.Stopped],
            [PlayerLifecycleState.Failed] = [PlayerLifecycleState.Preparing, PlayerLifecycleState.Idle, PlayerLifecycleState.Stopped],
        };

    private readonly object gate = new();
    private readonly HashSet<Guid> rememberedEventIds = [];
    private readonly Queue<Guid> rememberedEventOrder = [];
    private PlaybackMachineState current = new(PlayerLifecycleState.Stopped, null, 0, null);

    public PlaybackMachineState Current
    {
        get { lock (gate) return current; }
    }

    public Result<PlaybackMachineState> BeginLoad(Guid playbackId)
    {
        if (playbackId == Guid.Empty)
            return Result<PlaybackMachineState>.Failure(new Error("player.invalid_playback_id", "Playback id is required."));
        lock (gate)
        {
            if (!CanTransition(current.State, PlayerLifecycleState.Preparing))
                return Result<PlaybackMachineState>.Failure(IllegalTransition(current.State, PlayerLifecycleState.Preparing));
            current = current with
            {
                State = PlayerLifecycleState.Preparing,
                PlaybackId = playbackId,
                Revision = current.Revision + 1,
                Failure = null,
            };
            return Result<PlaybackMachineState>.Success(current);
        }
    }

    public PlayerEventTransition Apply(PlayerEvent playerEvent)
    {
        ArgumentNullException.ThrowIfNull(playerEvent);
        lock (gate)
        {
            if (rememberedEventIds.Contains(playerEvent.EventId))
                return Transition(PlayerEventDisposition.Duplicate, Error.None);
            Remember(playerEvent.EventId);

            if (IsStalePlayback(playerEvent) || current.LastEventAt is { } last && playerEvent.OccurredAt < last)
                return Transition(PlayerEventDisposition.Stale, Error.None);

            var target = TargetState(playerEvent);
            if (target is null)
            {
                current = current with { Revision = current.Revision + 1, LastEventAt = playerEvent.OccurredAt };
                return Transition(PlayerEventDisposition.Applied, Error.None);
            }

            if (!CanTransition(current.State, target.Value))
                return Transition(PlayerEventDisposition.Rejected, IllegalTransition(current.State, target.Value));

            var playbackId = target is PlayerLifecycleState.Stopped or PlayerLifecycleState.Idle
                ? null
                : playerEvent.PlaybackId ?? current.PlaybackId;
            var failure = playerEvent is PlaybackFailedEvent failed ? failed.Failure :
                target == PlayerLifecycleState.Failed ? current.Failure : null;
            current = new PlaybackMachineState(target.Value, playbackId, current.Revision + 1, playerEvent.OccurredAt, failure);
            return Transition(PlayerEventDisposition.Applied, Error.None);
        }
    }

    private bool IsStalePlayback(PlayerEvent playerEvent) =>
        playerEvent.PlaybackId is { } eventPlaybackId && current.PlaybackId is { } activePlaybackId && eventPlaybackId != activePlaybackId;

    private static PlayerLifecycleState? TargetState(PlayerEvent playerEvent) => playerEvent switch
    {
        PlayerStartedEvent => PlayerLifecycleState.Idle,
        PlayerStateChangedEvent changed => changed.Current,
        PlaybackStartedEvent => PlayerLifecycleState.Playing,
        PlaybackPausedEvent => PlayerLifecycleState.Paused,
        PlaybackEndedEvent => PlayerLifecycleState.Ended,
        PlaybackFailedEvent => PlayerLifecycleState.Failed,
        PlayerTracksChangedEvent => null,
        _ => null,
    };

    private static bool CanTransition(PlayerLifecycleState from, PlayerLifecycleState to) =>
        from == to || AllowedTransitions[from].Contains(to);

    private static Error IllegalTransition(PlayerLifecycleState from, PlayerLifecycleState to) =>
        new("player.invalid_transition", $"Cannot transition player from {from} to {to}.");

    private PlayerEventTransition Transition(PlayerEventDisposition disposition, Error error) => new(disposition, current, error);

    private void Remember(Guid eventId)
    {
        rememberedEventIds.Add(eventId);
        rememberedEventOrder.Enqueue(eventId);
        if (rememberedEventOrder.Count <= RememberedEventLimit) return;
        rememberedEventIds.Remove(rememberedEventOrder.Dequeue());
    }
}
