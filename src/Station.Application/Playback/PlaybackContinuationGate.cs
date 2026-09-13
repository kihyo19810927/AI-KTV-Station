namespace Station.Application.Playback;

public sealed class PlaybackContinuationGate
{
    // A process restart must never revive an old queue without an explicit
    // host action. The user can resume through PlaybackControlService.PlayAsync.
    private int suspended = 1;
    public bool IsSuspended => Volatile.Read(ref suspended) == 1;
    public void Suspend() => Interlocked.Exchange(ref suspended, 1);
    public void Resume() => Interlocked.Exchange(ref suspended, 0);
}
