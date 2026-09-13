using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Queue;
using Station.Application.Search;
using Station.Desktop.Services;

namespace Station.Desktop.ViewModels;

public sealed class DesktopSongRequestViewModel : ObservableObject
{
    // A fixed small page keeps the visual tree bounded even when the catalog has
    // thousands of artists.  Artist lookup remains available through the two
    // explicit search buttons and group chips.
    private const int ArtistPageSize = 24;
    private readonly ISongSearchIndex search;
    private readonly IArtistBrowseService artistBrowse;
    private readonly RoomQueueService queue;
    private readonly HostRoomContext room;
    private string searchText = string.Empty;
    private string statusMessage = "输入关键字后选择按歌名或按歌手搜索，也可以按歌星浏览";
    private string miniPlayerText = "当前没有播放 · 队列为空";
    private bool showingArtists;
    private string artistGroup = string.Empty;
    private IReadOnlyList<ArtistBrowseItem> allArtists = [];
    private int artistPage = 1;

    public DesktopSongRequestViewModel(ISongSearchIndex search, IArtistBrowseService artistBrowse, RoomQueueService queue,
        HostRoomContext room, QueueManagementViewModel queueManagement)
    {
        this.search = search; this.artistBrowse = artistBrowse; this.queue = queue; this.room = room;
        QueueManagement = queueManagement;
        SearchTitleCommand = new AsyncRelayCommand(() => SearchAsync(SongSearchField.Title));
        SearchArtistCommand = new AsyncRelayCommand(() => SearchAsync(SongSearchField.Artist));
        SelectArtistGroupCommand = new AsyncRelayCommand<string>(LoadArtistsAsync);
        SelectArtistCommand = new AsyncRelayCommand<ArtistBrowseItem>(artist => SearchArtistAsync(artist.Name));
        RequestSongCommand = new AsyncRelayCommand<SongSearchItem>(RequestSongAsync);
        PreviousArtistPageCommand = new AsyncRelayCommand(() => ChangeArtistPageAsync(-1), () => artistPage > 1);
        NextArtistPageCommand = new AsyncRelayCommand(() => ChangeArtistPageAsync(1), () => artistPage < ArtistPageCount);
    }

    public ObservableCollection<SongSearchItem> Songs { get; } = [];
    public ObservableCollection<ArtistBrowseItem> Artists { get; } = [];
    public IReadOnlyList<string> ArtistGroups { get; } = ["全部", "华语男歌手", "华语女歌手", "华语组合", "欧美歌手", "日本歌手", "韩国歌手", "其他"];
    public QueueManagementViewModel QueueManagement { get; }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public string MiniPlayerText { get => miniPlayerText; private set => SetProperty(ref miniPlayerText, value); }
    public bool ShowingArtists { get => showingArtists; private set { if (SetProperty(ref showingArtists, value)) RaisePropertyChanged(nameof(ShowingSongs)); } }
    public bool ShowingSongs => !ShowingArtists;
    public string ArtistGroup { get => artistGroup; private set => SetProperty(ref artistGroup, value); }
    public ICommand SearchTitleCommand { get; }
    public ICommand SearchArtistCommand { get; }
    public ICommand SelectArtistGroupCommand { get; }
    public ICommand SelectArtistCommand { get; }
    public ICommand RequestSongCommand { get; }
    public AsyncRelayCommand PreviousArtistPageCommand { get; }
    public AsyncRelayCommand NextArtistPageCommand { get; }
    public int ArtistPageCount => Math.Max(1, (int)Math.Ceiling(allArtists.Count / (double)ArtistPageSize));
    public string ArtistPageText => allArtists.Count == 0
        ? "暂无歌星"
        : $"第 {artistPage} / {ArtistPageCount} 页 · 共 {allArtists.Count:N0} 位歌星";

    public async Task InitializeAsync()
    {
        await SearchAsync(SongSearchField.Any);
        await RefreshQueueSummaryAsync();
    }

    private async Task SearchAsync(SongSearchField field)
    {
        var result = await search.SearchAsync(new SongSearchQuery(SearchText.Trim(), 1, 100, Sort: SongSearchSort.Relevance, Field: field));
        if (result.IsFailure) { StatusMessage = $"搜索失败：{result.Error.Message}"; return; }
        Replace(Songs, result.Value.Items); ShowingArtists = false; StatusMessage = $"找到 {result.Value.Total:N0} 首歌曲";
    }

    private async Task SearchArtistAsync(string artist)
    {
        SearchText = artist;
        var result = await search.SearchAsync(new SongSearchQuery(
            artist, 1, 100, Sort: SongSearchSort.Relevance, Artist: artist, Field: SongSearchField.Artist));
        if (result.IsFailure) { StatusMessage = $"搜索失败：{result.Error.Message}"; return; }
        Replace(Songs, result.Value.Items);
        ShowingArtists = false;
        StatusMessage = $"歌手「{artist}」：{result.Value.Total:N0} 首歌曲";
    }

    private async Task LoadArtistsAsync(string group)
    {
        ArtistGroup = group == "全部" ? string.Empty : group;
        allArtists = await artistBrowse.ListAsync(ArtistGroup, 3_000);
        artistPage = 1;
        ShowArtistPage();
        ShowingArtists = true;
        StatusMessage = string.IsNullOrEmpty(ArtistGroup) ? $"按热度显示 {allArtists.Count:N0} 位歌星" : $"{ArtistGroup}：{allArtists.Count:N0} 位歌星";
    }

    private Task ChangeArtistPageAsync(int offset)
    {
        artistPage = Math.Clamp(artistPage + offset, 1, ArtistPageCount);
        ShowArtistPage();
        return Task.CompletedTask;
    }

    private void ShowArtistPage()
    {
        Replace(Artists, allArtists.Skip((artistPage - 1) * ArtistPageSize).Take(ArtistPageSize));
        RaisePropertyChanged(nameof(ArtistPageText));
        RaisePropertyChanged(nameof(ArtistPageCount));
        PreviousArtistPageCommand.NotifyCanExecuteChanged();
        NextArtistPageCommand.NotifyCanExecuteChanged();
    }

    private async Task RequestSongAsync(SongSearchItem song)
    {
        if (room.Identity is null) { StatusMessage = "请先开启房间"; return; }
        var result = await queue.RequestAsync(room.Identity, song.SongId);
        StatusMessage = result.IsSuccess ? $"已点播《{song.Title}》，正在探测媒体" : $"点歌失败：{result.Error.Message}";
        if (result.IsSuccess)
        {
            await QueueManagement.RefreshAsync();
            await RefreshQueueSummaryAsync();
        }
    }

    public async Task RefreshQueueSummaryAsync()
    {
        if (room.Identity is null) { MiniPlayerText = "请先开启房间后点歌"; return; }
        var result = await queue.ListAsync(room.Identity);
        if (result.IsFailure) return;
        var current = result.Value.FirstOrDefault(x => x.Status is Station.Domain.Models.QueueItemStatus.Playing or Station.Domain.Models.QueueItemStatus.Paused or Station.Domain.Models.QueueItemStatus.Preparing);
        var waiting = result.Value.Count(x => x.Status is Station.Domain.Models.QueueItemStatus.Probing or Station.Domain.Models.QueueItemStatus.Waiting or Station.Domain.Models.QueueItemStatus.ProbeFailed);
        MiniPlayerText = current is null
            ? $"等待开唱 · 已点 {waiting} 首"
            : $"正在播放：{current.Title} · 后面还有 {waiting} 首";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
