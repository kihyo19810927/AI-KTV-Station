using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Queue;
using Station.Application.Search;
using Station.Desktop.Services;

namespace Station.Desktop.ViewModels;

public sealed class DesktopSongRequestViewModel : ObservableObject
{
    private readonly ISongSearchIndex search;
    private readonly IArtistBrowseService artistBrowse;
    private readonly RoomQueueService queue;
    private readonly HostRoomContext room;
    private string searchText = string.Empty;
    private string statusMessage = "输入歌名或歌手后搜索，也可以按歌星浏览";
    private bool showingArtists;
    private string artistGroup = string.Empty;

    public DesktopSongRequestViewModel(ISongSearchIndex search, IArtistBrowseService artistBrowse, RoomQueueService queue,
        HostRoomContext room, QueueManagementViewModel queueManagement)
    {
        this.search = search; this.artistBrowse = artistBrowse; this.queue = queue; this.room = room;
        QueueManagement = queueManagement;
        SearchTitleCommand = new AsyncRelayCommand(() => SearchAsync(SongSearchField.Title));
        SearchArtistCommand = new AsyncRelayCommand(() => SearchAsync(SongSearchField.Artist));
        ShowSongsCommand = new AsyncRelayCommand(() => SearchAsync(SongSearchField.Any));
        ShowArtistsCommand = new AsyncRelayCommand(() => LoadArtistsAsync(string.Empty));
        SelectArtistGroupCommand = new AsyncRelayCommand<string>(LoadArtistsAsync);
        SelectArtistCommand = new AsyncRelayCommand<ArtistBrowseItem>(artist => SearchArtistAsync(artist.Name));
        RequestSongCommand = new AsyncRelayCommand<SongSearchItem>(RequestSongAsync);
    }

    public ObservableCollection<SongSearchItem> Songs { get; } = [];
    public ObservableCollection<ArtistBrowseItem> Artists { get; } = [];
    public IReadOnlyList<string> ArtistGroups { get; } = ["全部", "华语男歌手", "华语女歌手", "华语组合", "欧美歌手", "日本歌手", "韩国歌手", "其他"];
    public QueueManagementViewModel QueueManagement { get; }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool ShowingArtists { get => showingArtists; private set { if (SetProperty(ref showingArtists, value)) RaisePropertyChanged(nameof(ShowingSongs)); } }
    public bool ShowingSongs => !ShowingArtists;
    public string ArtistGroup { get => artistGroup; private set => SetProperty(ref artistGroup, value); }
    public ICommand SearchTitleCommand { get; }
    public ICommand SearchArtistCommand { get; }
    public ICommand ShowSongsCommand { get; }
    public ICommand ShowArtistsCommand { get; }
    public ICommand SelectArtistGroupCommand { get; }
    public ICommand SelectArtistCommand { get; }
    public ICommand RequestSongCommand { get; }

    public Task InitializeAsync() => SearchAsync(SongSearchField.Any);

    private async Task SearchAsync(SongSearchField field)
    {
        var result = await search.SearchAsync(new SongSearchQuery(SearchText.Trim(), 1, 100, Sort: SongSearchSort.Relevance, Field: field));
        if (result.IsFailure) { StatusMessage = $"搜索失败：{result.Error.Message}"; return; }
        Replace(Songs, result.Value.Items); ShowingArtists = false; StatusMessage = $"找到 {result.Value.Total:N0} 首歌曲";
    }

    private async Task SearchArtistAsync(string artist)
    {
        SearchText = artist;
        await SearchAsync(SongSearchField.Artist);
    }

    private async Task LoadArtistsAsync(string group)
    {
        ArtistGroup = group == "全部" ? string.Empty : group;
        var rows = await artistBrowse.ListAsync(ArtistGroup, 300);
        Replace(Artists, rows); ShowingArtists = true;
        StatusMessage = string.IsNullOrEmpty(ArtistGroup) ? $"按热度显示 {rows.Count} 位歌手" : $"{ArtistGroup}：{rows.Count} 位歌手";
    }

    private async Task RequestSongAsync(SongSearchItem song)
    {
        if (room.Identity is null) { StatusMessage = "请先开启房间"; return; }
        var result = await queue.RequestAsync(room.Identity, song.SongId);
        StatusMessage = result.IsSuccess ? $"已点播《{song.Title}》，正在探测媒体" : $"点歌失败：{result.Error.Message}";
        if (result.IsSuccess) await QueueManagement.RefreshAsync();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
