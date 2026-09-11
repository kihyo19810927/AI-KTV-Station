using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Station.Application.Catalog;
using Station.Application.Search;
using Station.Desktop.Services;

namespace Station.Desktop.ViewModels;

public sealed class CatalogManagementViewModel : ObservableObject
{
    private readonly ISongSearchIndex search;
    private readonly ICatalogAdminService catalog;
    private string searchText = string.Empty;
    private string statusMessage = "搜索曲库或导入 JSON/JSONL 索引";
    private SongAdminDetails? selectedSong;
    private string editTitle = string.Empty;
    private string? editLanguage;
    private string? editCategory;
    private string? editQuality;
    private int? editYear;
    private readonly ICatalogJsonImportService importer;
    private readonly ICatalogImportFilePicker importFilePicker;
    private string importFilePath = string.Empty;
    private string importMountRoot = string.Empty;
    private bool isImporting;

    public CatalogManagementViewModel(ISongSearchIndex search, ICatalogAdminService catalog, ICatalogJsonImportService importer, ICatalogImportFilePicker importFilePicker)
    {
        this.search = search; this.catalog = catalog; this.importer = importer; this.importFilePicker = importFilePicker;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SelectSongCommand = new AsyncRelayCommand<SongSearchItem>(item => SelectSongAsync(item.SongId));
        SaveMetadataCommand = new AsyncRelayCommand(SaveMetadataAsync);
        var bundledIndex = Path.Combine(AppContext.BaseDirectory, "initial-library", "ktv_songs_index.jsonl");
        if (File.Exists(bundledIndex)) importFilePath = bundledIndex;
        SelectImportFileCommand = new RelayCommand<object?>(_ => SelectImportFile());
        ImportCatalogCommand = new AsyncRelayCommand(ImportCatalogAsync, () => !IsImporting);
    }

    public ObservableCollection<SongSearchItem> Songs { get; } = [];
    public ICommand SearchCommand { get; }
    public ICommand SelectSongCommand { get; }
    public ICommand SaveMetadataCommand { get; }
    public ICommand SelectImportFileCommand { get; }
    public ICommand ImportCatalogCommand { get; }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public SongAdminDetails? SelectedSong { get => selectedSong; private set => SetProperty(ref selectedSong, value); }
    public string EditTitle { get => editTitle; set => SetProperty(ref editTitle, value); }
    public string? EditLanguage { get => editLanguage; set => SetProperty(ref editLanguage, value); }
    public string? EditCategory { get => editCategory; set => SetProperty(ref editCategory, value); }
    public string? EditQuality { get => editQuality; set => SetProperty(ref editQuality, value); }
    public int? EditYear { get => editYear; set => SetProperty(ref editYear, value); }
    public string ImportFilePath { get => importFilePath; set => SetProperty(ref importFilePath, value); }
    public string ImportMountRoot { get => importMountRoot; set => SetProperty(ref importMountRoot, value); }
    public bool IsImporting { get => isImporting; private set { if (SetProperty(ref isImporting, value)) ((AsyncRelayCommand)ImportCatalogCommand).NotifyCanExecuteChanged(); } }

    public Task InitializeAsync() => SearchAsync();

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

    private void SelectImportFile() { var selected = importFilePicker.Pick(); if (!string.IsNullOrWhiteSpace(selected)) ImportFilePath = selected; }

    private async Task ImportCatalogAsync()
    {
        IsImporting = true; StatusMessage = "正在增量导入曲库…";
        var progress = new Progress<CatalogImportProgress>(x => StatusMessage = $"已读取 {x.Read:N0} · 新增 {x.Added:N0} · 跳过 {x.Skipped:N0} · 错误 {x.Errors:N0}");
        try
        {
            var result = await importer.ImportAsync(ImportFilePath, ImportMountRoot, progress);
            if (result.IsSuccess)
            {
                await SearchAsync();
                StatusMessage = $"导入完成：新增 {result.Value.Added:N0}，跳过 {result.Value.Skipped:N0}，错误 {result.Value.Errors:N0}";
            }
            else StatusMessage = result.Error.Message;
        }
        finally { IsImporting = false; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
