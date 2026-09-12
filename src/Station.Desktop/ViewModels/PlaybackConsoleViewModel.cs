using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Common;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Desktop.Services;
using Station.Domain.Models;

namespace Station.Desktop.ViewModels;

public sealed class PlaybackConsoleViewModel : ObservableObject, IDisposable
{
    private readonly PlaybackControlService controls;
    private readonly IRoomQueueService? queue;
    private readonly HostRoomContext? roomContext;
    private CancellationTokenSource? volumeChange;
    private bool applyingSnapshot;
    private PlayerLifecycleState state = PlayerLifecycleState.Stopped;
    private double volume = 80;
    private double positionSeconds;
    private double durationSeconds = 1;
    private string statusMessage = "播放器尚未启动";
    private string currentSongTitle = "暂无歌曲";
    private string currentSongArtists = string.Empty;
    private DateTimeOffset lastProgressUpdate = DateTimeOffset.UtcNow;

    public PlaybackConsoleViewModel(PlaybackControlService controls)
        : this(controls, null, null)
    {
    }

    public PlaybackConsoleViewModel(PlaybackControlService controls, IRoomQueueService? queue, HostRoomContext? roomContext)
    {
        this.controls = controls;
        this.queue = queue;
        this.roomContext = roomContext;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        PlayCommand = new AsyncRelayCommand(() => ApplyAsync(controls.PlayAsync()));
        PauseCommand = new AsyncRelayCommand(() => ApplyAsync(controls.PauseAsync()));
        ApplyVolumeCommand = new AsyncRelayCommand(() => ApplyAsync(controls.SetVolumeAsync(Volume)));
        SeekCommand = new AsyncRelayCommand(() => ApplyAsync(controls.SeekAsync(TimeSpan.FromSeconds(PositionSeconds))));
        SelectAudioCommand = new AsyncRelayCommand<int>(id => ApplyAsync(controls.SelectAudioAsync(id)));
        SelectSubtitleCommand = new AsyncRelayCommand<int?>(id => ApplyAsync(controls.SelectSubtitleAsync(id)));
    }

    public ICommand RefreshCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ApplyVolumeCommand { get; }
    public ICommand SeekCommand { get; }
    public ICommand SelectAudioCommand { get; }
    public ICommand SelectSubtitleCommand { get; }
    public PlayerLifecycleState State { get => state; private set => SetProperty(ref state, value); }
    public double Volume
    {
        get => volume;
        set
        {
            if (!SetProperty(ref volume, Math.Clamp(value, 0, 100)) || applyingSnapshot) return;
            if (State is PlayerLifecycleState.Playing or PlayerLifecycleState.Paused) ScheduleVolumeChange();
        }
    }
    public double PositionSeconds { get => positionSeconds; set => SetProperty(ref positionSeconds, value); }
    public double DurationSeconds { get => durationSeconds; private set => SetProperty(ref durationSeconds, Math.Max(1, value)); }
    public string PositionText => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {TimeSpan.FromSeconds(DurationSeconds):mm\\:ss}";
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public string CurrentSongTitle { get => currentSongTitle; private set => SetProperty(ref currentSongTitle, value); }
    public string CurrentSongArtists { get => currentSongArtists; private set => SetProperty(ref currentSongArtists, value); }
    public ObservableCollection<PlayerTrack> AudioTracks { get; } = [];
    public ObservableCollection<PlayerTrack> SubtitleTracks { get; } = [];

    public async Task RefreshAsync()
    {
        var progress = await controls.GetProgressAsync();
        if (progress.IsFailure) { ShowError(progress.Error); return; }
        var snapshot = await controls.GetSnapshotAsync();
        if (snapshot.IsFailure) { ShowError(snapshot.Error); return; }
        Apply(snapshot.Value);
        await RefreshCurrentSongAsync().ConfigureAwait(true);
    }

    public void AdvanceLocalProgress()
    {
        if (State != PlayerLifecycleState.Playing) { lastProgressUpdate = DateTimeOffset.UtcNow; return; }
        var now = DateTimeOffset.UtcNow;
        var elapsed = Math.Clamp((now - lastProgressUpdate).TotalSeconds, 0, 1);
        lastProgressUpdate = now;
        if (elapsed <= 0 || PositionSeconds >= DurationSeconds) return;
        PositionSeconds = Math.Min(DurationSeconds, PositionSeconds + elapsed);
        RaisePropertyChanged(nameof(PositionText));
    }

    private async Task ApplyAsync(Task<Result<PlayerSnapshot>> operation)
    {
        var result = await operation;
        if (result.IsFailure) { ShowError(result.Error); return; }
        Apply(result.Value);
    }

    private void Apply(PlayerSnapshot snapshot)
    {
        applyingSnapshot = true;
        try
        {
            State = snapshot.State;
            DurationSeconds = snapshot.Duration?.TotalSeconds ?? 1;
            Volume = snapshot.Volume;
            PositionSeconds = Math.Clamp(snapshot.Position.TotalSeconds, 0, DurationSeconds);
        }
        finally { applyingSnapshot = false; }
        RaisePropertyChanged(nameof(PositionText));
        lastProgressUpdate = DateTimeOffset.UtcNow;
        Replace(AudioTracks, snapshot.Tracks.Where(x => x.Type == MediaTrackType.Audio));
        Replace(SubtitleTracks, snapshot.Tracks.Where(x => x.Type == MediaTrackType.Subtitle));
        StatusMessage = snapshot.PlaybackId is null ? "当前没有播放任务" : snapshot.State switch { PlayerLifecycleState.Playing => "正在播放", PlayerLifecycleState.Paused => "已暂停", _ => snapshot.State.ToString() };
    }

    private void ShowError(Error error) => StatusMessage = $"操作未完成：{error.Message}";
    private async Task RefreshCurrentSongAsync()
    {
        if (queue is null || roomContext?.Identity is null) return;
        var result = await queue.ListAsync(roomContext.Identity).ConfigureAwait(true);
        if (result.IsFailure) return;
        var current = result.Value.FirstOrDefault(x => x.Status is QueueItemStatus.Playing or QueueItemStatus.Paused or QueueItemStatus.Preparing);
        CurrentSongTitle = current?.Title ?? "暂无歌曲";
        CurrentSongArtists = current?.Artists ?? string.Empty;
    }

    private void ScheduleVolumeChange()
    {
        volumeChange?.Cancel();
        volumeChange?.Dispose();
        var source = volumeChange = new CancellationTokenSource();
        _ = ApplyVolumeAfterDelayAsync(source);
    }

    private async Task ApplyVolumeAfterDelayAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(150, source.Token);
            var result = await controls.SetVolumeAsync(Volume, source.Token);
            if (result.IsFailure && !source.IsCancellationRequested) ShowError(result.Error);
            else if (result.IsSuccess && !source.IsCancellationRequested) Apply(result.Value);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(volumeChange, source))
            {
                volumeChange = null;
                source.Dispose();
            }
        }
    }

    public void Dispose()
    {
        volumeChange?.Cancel();
        volumeChange?.Dispose();
        volumeChange = null;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
