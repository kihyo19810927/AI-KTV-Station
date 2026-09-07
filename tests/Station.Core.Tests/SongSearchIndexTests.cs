using Microsoft.EntityFrameworkCore;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class SongSearchIndexTests
{
    [Fact]
    public async Task Searches_title_artist_pinyin_initials_and_filters_without_paths()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        await fixture.AddAsync("夜曲", "周杰伦", "国语", "流行", "1080P", 2005);
        await fixture.AddAsync("月光", "王心凌", "国语", "经典", "4K", 2024);
        await fixture.AddAsync("海闊天空", "Beyond", "粤语", "摇滚", "1080P", 1993);
        await fixture.Index.RebuildAsync();

        Assert.Equal("夜曲", Assert.Single((await fixture.Search("夜曲")).Items).Title);
        Assert.Equal("夜曲", Assert.Single((await fixture.Search("zhoujie")).Items).Title);
        Assert.Equal("夜曲", Assert.Single((await fixture.Search("zjl")).Items).Title);
        Assert.Equal("海闊天空", Assert.Single((await fixture.Search("海阔天空")).Items).Title);
        var filtered = await fixture.Index.SearchAsync(new SongSearchQuery(PageSize: 10, Language: "国语", Category: "经典", Quality: "4K", YearFrom: 2020));
        Assert.True(filtered.IsSuccess);
        Assert.Equal("月光", Assert.Single(filtered.Value.Items).Title);
        Assert.DoesNotContain(typeof(SongSearchItem).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Supports_stable_paging_sorting_update_delete_and_query_validation()
    {
        await using var fixture = await SearchFixture.CreateAsync();
        var older = await fixture.AddAsync("同名歌", "甲", "国语", "流行", "1080P", 2000);
        await fixture.AddAsync("同名歌 新版", "乙", "国语", "流行", "4K", 2025);
        await fixture.Index.RebuildAsync();

        var first = await fixture.Index.SearchAsync(new SongSearchQuery("同名歌", Page: 1, PageSize: 1, Sort: SongSearchSort.YearDescending));
        var second = await fixture.Index.SearchAsync(new SongSearchQuery("同名歌", Page: 2, PageSize: 1, Sort: SongSearchSort.YearDescending));
        Assert.Equal(2, first.Value.Total);
        Assert.Equal(2025, first.Value.Items.Single().Year);
        Assert.Equal(2000, second.Value.Items.Single().Year);

        older.Title = "旧歌新名";
        SongSearchKeyUpdater.Update(older, fixture.Normalizer);
        await fixture.Database.SaveChangesAsync();
        await fixture.Index.UpsertAsync([older.Id]);
        Assert.Empty((await fixture.Search("同名歌", yearTo: 2000)).Items);
        Assert.Equal(older.Id, Assert.Single((await fixture.Search("旧歌新名")).Items).SongId);

        fixture.Database.Songs.Remove(older);
        await fixture.Database.SaveChangesAsync();
        await fixture.Index.UpsertAsync([older.Id]);
        Assert.Empty((await fixture.Search("旧歌新名")).Items);
        Assert.Equal("search.invalid_page_size", (await fixture.Index.SearchAsync(new SongSearchQuery(PageSize: 101))).Error.Code);
        Assert.True((await fixture.Index.SearchAsync(new SongSearchQuery("\" OR *", PageSize: 10))).IsSuccess);
        Assert.Empty((await fixture.Search("🎤")).Items);
    }

    private sealed class SearchFixture : IAsyncDisposable
    {
        private readonly string databasePath;
        public StationDbContext Database { get; }
        public ToolGoodSearchTextNormalizer Normalizer { get; } = new();
        public SqliteSongSearchIndex Index { get; }

        private SearchFixture(string databasePath, StationDbContext database)
        {
            this.databasePath = databasePath;
            Database = database;
            Index = new SqliteSongSearchIndex(database, Normalizer);
        }

        public static async Task<SearchFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ai-ktv-search-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            var database = new StationDbContext(options);
            await database.Database.MigrateAsync();
            return new SearchFixture(path, database);
        }

        public async Task<Song> AddAsync(string title, string artistName, string language, string category, string quality, int year)
        {
            var song = new Song
            {
                Title = title,
                Language = language,
                Category = category,
                Quality = quality,
                Year = year,
                Availability = AvailabilityStatus.Available,
                Artists = [new SongArtist { Artist = new Artist { Name = artistName } }],
            };
            SongSearchKeyUpdater.Update(song, Normalizer);
            Database.Songs.Add(song);
            await Database.SaveChangesAsync();
            return song;
        }

        public async Task<SongSearchPage> Search(string text, int? yearTo = null)
        {
            var result = await Index.SearchAsync(new SongSearchQuery(text, PageSize: 10, YearTo: yearTo));
            Assert.True(result.IsSuccess, result.Error.Code);
            return result.Value;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
