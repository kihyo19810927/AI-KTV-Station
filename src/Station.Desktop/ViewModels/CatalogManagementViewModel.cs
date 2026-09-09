using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Catalog;
using Station.Application.MediaSources;
using Station.Application.Scanning;
using Station.Application.Search;

namespace Station.Desktop.ViewModels;

public sealed class CatalogManagementViewModel : ObservableObject
{
    private readonly ISongSearchIndex search;
    private readonly ICatalogAdminService catalog;
    private readonly IMediaSourceService sources;
    private readonly ICatalogScanService scans;
    private CancellationTokenSource? scanCancellation;
    private string searchText = string.Empty;
    private string statusMessage = "搜索曲库或添加一个年度目录";
    private SongAdminDetails? selectedSong;
    private MediaSourceAdminDetails? selectedSource;
    private bool isScanning;
    private string editTitle = string.Empty;
    private string? editLanguage;
    private string? editCategory;
    private string? editQuality;
    private int? editYear;
    private string sourceName = string.Empty;
    private string sourcePath = string.Empty;

    public CatalogManagementViewModel(ISongSearchIndex search, ICatalogAdminService catalog, IMediaSourceService sources, ICatalogScanService scans)
    {
        this.search = search; this.catalog = catalog; this.sources = sources; this.scans = scans;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SelectSongCommand = new AsyncRelayCommand<SongSearchItem>(item => SelectSongAsync(item.SongId));
        SaveMetadataCommand = new AsyncRelayCommand(SaveMetadataAsync);
        RefreshSourcesCommand = new AsyncRelayCommand(RefreshSourcesAsync);
        AddSourceCommand = new AsyncRelayCommand(AddSourceAsync);
        StartScanCommand = new AsyncRelayCommand(StartScanAsync);
        CancelScanCommand = new RelayCommand<object?>(_ => scanCancellation?.Cancel(), _ => IsScanning);
    }

    public ObservableCollection<SongSearchItem> Songs { get; } = [];
    public ObservableCollection<MediaSourceAdminDetails> Sources { get; } = [];
    public ICommand SearchCommand { get; }
    public ICommand SelectSongCommand { get; }
    public ICommand SaveMetadataCommand { get; }
    public ICommand RefreshSourcesCommand { get; }
    public ICommand AddSourceCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public SongAdminDetails? SelectedSong { get => selectedSong; private set => SetProperty(ref selectedSong, value); }
    public MediaSourceAdminDetails? SelectedSource { get => selectedSource; set => SetProperty(ref selectedSource, value); }
    public bool IsScanning { get => isScanning; private set { if (SetProperty(ref isScanning, value)) ((RelayCommand<object?>)CancelScanCommand).NotifyCanExecuteChanged(); } }
    public string EditTitle { get => editTitle; set => SetProperty(ref editTitle, value); }
    public string? EditLanguage { get => editLanguage; set => SetProperty(ref editLanguage, value); }
    public string? EditCategory { get => editCategory; set => SetProperty(ref editCategory, value); }
    public string? EditQuality { get => editQuality; set => SetProperty(ref editQuality, value); }
    public int? EditYear { get => editYear; set => SetProperty(ref editYear, value); }
    public string SourceName { get => sourceName; set => SetProperty(ref sourceName, value); }
    public string SourcePath { get => sourcePath; set => SetProperty(ref sourcePath, value); }

    public async Task InitializeAsync() { await RefreshSourcesAsync(); await SearchAsync(); }

    public async Task SearchAsync()
    {
        var result = await search.SearchAsync(new SongSearchQuery(SearchText, 1, 100, Sort: SongSearchSort.Title));
        if (result.IsFailure) { StatusMessage = $"搜索失败：{result.Error.Message}"; return; }
        Replace(Songs, result.Value.Items);
        StatusMessage = $"找到 {result.Value.Total} 首歌曲";
    }

    private async Task SelectSongAsync(Guid songId)
    {
        var result = await catalog.GetAsync(songId);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        SelectedSong = result.Value; EditTitle = result.Value.Title; EditLanguage = result.Value.Language; EditCategory = result.Value.Category; EditYear = result.Value.Year; EditQuality = result.Value.Quality;
    }

    private async Task SaveMetadataAsync()
    {
        if (SelectedSong is null) { StatusMessage = "请先选择歌曲"; return; }
        var result = await catalog.UpdateAsync(SelectedSong.Id, new SongMetadataUpdate(EditTitle, EditLanguage, EditCategory, EditYear, EditQuality));
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        SelectedSong = result.Value; await SearchAsync(); StatusMessage = "人工修正已保存，搜索索引已刷新";
    }

    private async Task RefreshSourcesAsync()
    {
        Replace(Sources, await sources.ListAdminAsync());
        if (SelectedSource is null || Sources.All(x => x.Id != SelectedSource.Id)) SelectedSource = Sources.FirstOrDefault();
    }

    private async Task AddSourceAsync()
    {
        var result = await sources.AddAsync(SourceName, SourcePath);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        await RefreshSourcesAsync(); SelectedSource = Sources.Single(x => x.Id == result.Value.Id); StatusMessage = "媒体源已添加，可单独扫描该目录";
    }

    private async Task StartScanAsync()
    {
        if (SelectedSource is null || IsScanning) { StatusMessage = "请选择要扫描的年度或月份目录"; return; }
        scanCancellation = new CancellationTokenSource(); IsScanning = true;
        var progress = new Progress<MediaScanProgress>(x => StatusMessage = $"扫描中：发现 {x.DiscoveredFiles}，更新 {x.UpdatedFiles}，错误 {x.ErrorCount}");
        try
        {
            var result = await scans.ScanAsync(SelectedSource.Id, progress, scanCancellation.Token);
            var completionMessage = result.IsSuccess
                ? result.Value.Status == Station.Domain.Models.ScanStatus.Cancelled
                    ? "扫描已取消，检查点和已有索引已保留"
                    : $"扫描完成：发现 {result.Value.DiscoveredFiles}，更新 {result.Value.UpdatedFiles}"
                : result.Error.Message;
            await SearchAsync(); StatusMessage = completionMessage;
        }
        catch (OperationCanceledException) { StatusMessage = "扫描已取消，检查点和已有索引已保留"; }
        finally { scanCancellation.Dispose(); scanCancellation = null; IsScanning = false; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
