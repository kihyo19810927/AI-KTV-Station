using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.MediaSources;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Desktop.ViewModels;
using Station.Domain.Models;

namespace Station.Desktop.Tests;

public sealed class CatalogManagementViewModelTests
{
    [Fact]
    public async Task Initialization_lists_sources_and_searches_existing_index_without_scanning()
    {
        var search = new FakeSearch(); var source = new FakeSources(); var scans = new FakeScans();
        var viewModel = new CatalogManagementViewModel(search, new FakeCatalog(), source, scans);

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Sources); Assert.Single(viewModel.Songs); Assert.Equal(1, search.Calls); Assert.Equal(0, scans.Calls);
    }

    [Fact]
    public async Task Adding_completed_year_directory_does_not_implicitly_scan_other_sources()
    {
        var sources = new FakeSources(); var scans = new FakeScans();
        var viewModel = new CatalogManagementViewModel(new FakeSearch(), new FakeCatalog(), sources, scans) { SourceName = "2016年", SourcePath = @"E:\fixture\16年" };

        viewModel.AddSourceCommand.Execute(null);
        await sources.Added.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(@"E:\fixture\16年", sources.AddedPath); Assert.Equal(0, scans.Calls);
    }

    [Fact]
    public void Scan_is_disabled_until_a_registered_source_is_selected()
    {
        var viewModel = new CatalogManagementViewModel(new FakeSearch(), new FakeCatalog(), new EmptySources(), new FakeScans());

        Assert.False(viewModel.StartScanCommand.CanExecute(null));
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
    private sealed class FakeSources : IMediaSourceService
    {
        private readonly List<MediaSourceAdminDetails> items = [new(Guid.NewGuid(), "现有年度", @"E:\fixture\existing", true, AvailabilityStatus.Available)];
        public TaskCompletionSource Added { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? AddedPath { get; private set; }
        public Task<Result<MediaSourceAdminDetails>> AddAsync(string name, string rootPath, CancellationToken cancellationToken = default) { AddedPath = rootPath; var item = new MediaSourceAdminDetails(Guid.NewGuid(), name, rootPath, true, AvailabilityStatus.Available); items.Add(item); Added.TrySetResult(); return Task.FromResult(Result<MediaSourceAdminDetails>.Success(item)); }
        public Task<Result<MediaSourceAdminDetails>> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MediaSourceSummary>> ListPublicAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MediaSourceAdminDetails>> ListAdminAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSourceAdminDetails>>(items.ToArray());
    }
    private sealed class FakeScans : ICatalogScanService
    {
        public int Calls { get; private set; }
        public Task<Result<ScanRun>> ScanAsync(Guid mediaSourceId, IProgress<MediaScanProgress>? progress = null, CancellationToken cancellationToken = default) { Calls++; throw new NotSupportedException(); }
    }
    private sealed class EmptySources : IMediaSourceService
    {
        public Task<Result<MediaSourceAdminDetails>> AddAsync(string name, string rootPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<MediaSourceAdminDetails>> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MediaSourceSummary>> ListPublicAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSourceSummary>>([]);
        public Task<IReadOnlyList<MediaSourceAdminDetails>> ListAdminAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSourceAdminDetails>>([]);
    }
}
