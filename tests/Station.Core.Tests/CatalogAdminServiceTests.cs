using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Catalog;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class CatalogAdminServiceTests
{
    [Fact]
    public async Task Manual_metadata_update_persists_and_refreshes_derived_search_index()
    {
        await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
        var song = new Song { Title = "旧标题", Availability = AvailabilityStatus.Available, Artists = [new SongArtist { Artist = new Artist { Name = "歌手" } }] };
        database.Songs.Add(song); await database.SaveChangesAsync();
        var index = new RecordingSearchIndex();
        var service = new CatalogAdminService(new EfCatalogAdminRepository(database), new ToolGoodSearchTextNormalizer(), index);

        var result = await service.UpdateAsync(song.Id, new SongMetadataUpdate(" 新标题 ", "国语", "流行", 2026, "1080P"));

        Assert.True(result.IsSuccess);
        Assert.Equal("新标题", (await database.Songs.SingleAsync()).Title);
        Assert.Equal("xinbiaoti", song.TitlePinyin);
        Assert.Equal([song.Id], index.Upserted);
        Assert.DoesNotContain(typeof(SongAdminDetails).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("", null, "catalog.title_invalid")]
    [InlineData("标题", 1899, "catalog.year_invalid")]
    [InlineData("标题", 2101, "catalog.year_invalid")]
    public async Task Invalid_manual_metadata_is_rejected_without_writes(string title, int? year, string code)
    {
        await using var database = CreateDatabase(); await database.Database.EnsureCreatedAsync();
        var index = new RecordingSearchIndex();
        var result = await new CatalogAdminService(new EfCatalogAdminRepository(database), new ToolGoodSearchTextNormalizer(), index).UpdateAsync(Guid.NewGuid(), new SongMetadataUpdate(title, null, null, year, null));
        Assert.Equal(code, result.Error.Code); Assert.Empty(index.Upserted);
    }

    private static StationDbContext CreateDatabase() { var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); return new(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, true).Options); }
    private sealed class RecordingSearchIndex : ISongSearchIndex
    {
        public IReadOnlyCollection<Guid> Upserted { get; private set; } = [];
        public Task RebuildAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) { Upserted = songIds; return Task.CompletedTask; }
        public Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
