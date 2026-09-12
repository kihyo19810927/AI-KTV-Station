namespace Station.Application.Playback;

public sealed class PlaybackContinuationGate
{
    private int suspended;
    public bool IsSuspended => Volatile.Read(ref suspended) == 1;
    public void Suspend() => Interlocked.Exchange(ref suspended, 1);
    public void Resume() => Interlocked.Exchange(ref suspended, 0);
}
