using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Common;
using Station.Application.Playback;
using Station.Domain.Models;

namespace Station.Desktop.ViewModels;

public sealed class PlaybackConsoleViewModel : ObservableObject
{
    private readonly PlaybackControlService controls;
    private PlayerLifecycleState state = PlayerLifecycleState.Stopped;
    private double volume = 80;
    private double positionSeconds;
    private double durationSeconds = 1;
    private string statusMessage = "播放器尚未启动";

    public PlaybackConsoleViewModel(PlaybackControlService controls)
    {
        this.controls = controls;
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
    public double Volume { get => volume; set => SetProperty(ref volume, value); }
    public double PositionSeconds { get => positionSeconds; set => SetProperty(ref positionSeconds, value); }
    public double DurationSeconds { get => durationSeconds; private set => SetProperty(ref durationSeconds, Math.Max(1, value)); }
    public string PositionText => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {TimeSpan.FromSeconds(DurationSeconds):mm\\:ss}";
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public ObservableCollection<PlayerTrack> AudioTracks { get; } = [];
    public ObservableCollection<PlayerTrack> SubtitleTracks { get; } = [];

    public async Task RefreshAsync()
    {
        var progress = await controls.GetProgressAsync();
        if (progress.IsFailure) { ShowError(progress.Error); return; }
        var snapshot = await controls.GetSnapshotAsync();
        if (snapshot.IsFailure) { ShowError(snapshot.Error); return; }
        Apply(snapshot.Value);
    }

    private async Task ApplyAsync(Task<Result<PlayerSnapshot>> operation)
    {
        var result = await operation;
        if (result.IsFailure) { ShowError(result.Error); return; }
        Apply(result.Value);
    }

    private void Apply(PlayerSnapshot snapshot)
    {
        State = snapshot.State; Volume = snapshot.Volume; PositionSeconds = snapshot.Position.TotalSeconds;
        DurationSeconds = snapshot.Duration?.TotalSeconds ?? 1; RaisePropertyChanged(nameof(PositionText));
        Replace(AudioTracks, snapshot.Tracks.Where(x => x.Type == MediaTrackType.Audio));
        Replace(SubtitleTracks, snapshot.Tracks.Where(x => x.Type == MediaTrackType.Subtitle));
        StatusMessage = snapshot.PlaybackId is null ? "当前没有播放任务" : snapshot.State switch { PlayerLifecycleState.Playing => "正在播放", PlayerLifecycleState.Paused => "已暂停", _ => snapshot.State.ToString() };
    }

    private void ShowError(Error error) => StatusMessage = $"操作未完成：{error.Message}";
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
