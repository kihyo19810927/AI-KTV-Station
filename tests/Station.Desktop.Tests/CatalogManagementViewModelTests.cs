using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Search;
using Station.Desktop.Services;
using Station.Desktop.ViewModels;
using Station.Domain.Models;

namespace Station.Desktop.Tests;

public sealed class CatalogManagementViewModelTests
{
    [Fact]
    public async Task Initialization_searches_existing_index_without_importing()
    {
        var search = new FakeSearch(); var importer = new FakeImporter();
        var viewModel = new CatalogManagementViewModel(search, new FakeCatalog(), importer, new FakePicker(null));

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Songs); Assert.Equal(1, search.Calls); Assert.Equal(0, importer.Calls);
    }

    [Fact]
    public async Task Import_uses_selected_json_and_mount_root_then_refreshes_search()
    {
        var search = new FakeSearch(); var importer = new FakeImporter();
        var viewModel = new CatalogManagementViewModel(search, new FakeCatalog(), importer, new FakePicker(@"D:\fixture\曲库.jsonl")) { ImportMountRoot = @"E:\KTV_TEST" };
        viewModel.SelectImportFileCommand.Execute(null);

        viewModel.ImportCatalogCommand.Execute(null);
        await importer.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(@"D:\fixture\曲库.jsonl", importer.IndexPath);
        Assert.Equal(@"E:\KTV_TEST", importer.MountRoot);
        Assert.Contains("新增 2", viewModel.StatusMessage);
        Assert.True(search.Calls >= 1);
    }

    private sealed class FakeSearch : ISongSearchIndex
    {
        public int Calls { get; private set; }
        public Task RebuildAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(Result<SongSearchPage>.Success(new([new(Guid.NewGuid(), "测试歌", "歌手", null, null, null, null, AvailabilityStatus.Available)], 1, 1, 100))); }
    }
    private sealed class FakeCatalog : ICatalogAdminService
    {
        public Task<Result<SongAdminDetails>> GetAsync(Guid songId, CancellationToken cancellationToken = default) => Task.FromResult(Result<SongAdminDetails>.Failure(new("unused", "unused")));
        public Task<Result<SongAdminDetails>> UpdateAsync(Guid songId, SongMetadataUpdate update, CancellationToken cancellationToken = default) => Task.FromResult(Result<SongAdminDetails>.Failure(new("unused", "unused")));
    }
    private sealed class FakeImporter : ICatalogJsonImportService
    {
        public int Calls { get; private set; }
        public string? IndexPath { get; private set; }
        public string? MountRoot { get; private set; }
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Result<CatalogImportResult>> ImportAsync(string indexPath, string mountRoot, IProgress<CatalogImportProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Calls++; IndexPath = indexPath; MountRoot = mountRoot; Completed.TrySetResult();
            return Task.FromResult(Result<CatalogImportResult>.Success(new(3, 2, 1, 0)));
        }
    }
    private sealed class FakePicker(string? path) : ICatalogImportFilePicker { public string? Pick() => path; }
}
