using Microsoft.EntityFrameworkCore;
using Station.Application.Search;
using Station.Infrastructure.Catalog;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Search;

namespace Station.Core.Tests;

public sealed class CatalogJsonImportServiceTests
{
    [Fact]
    public async Task Streams_camel_case_jsonl_and_incrementally_skips_existing_media()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ai-ktv-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "station.db");
        var indexPath = Path.Combine(root, "曲库索引.jsonl");
        await File.WriteAllLinesAsync(indexPath,
        [
            "{\"relativePath\":\"16年/周杰伦-夜曲-国语-流行.mkv\",\"artist\":\"周杰伦\",\"title\":\"夜曲\",\"language\":\"国语\",\"category\":\"流行\",\"sizeBytes\":123,\"durationMs\":10000}",
            "{\"relativePath\":\"17年/五月天-倔强-国语-摇滚.mpg\",\"artist\":\"五月天\",\"title\":\"倔强\",\"language\":\"国语\",\"category\":\"摇滚\",\"sizeBytes\":456,\"durationMs\":null}",
            "{\"relativePath\":\"17年/周杰伦-晴天-国语-流行.mkv\",\"artist\":\"周杰伦\",\"title\":\"晴天\",\"language\":\"国语\",\"category\":\"流行\",\"sizeBytes\":789,\"durationMs\":null}"
        ]);

        try
        {
            var options = new DbContextOptionsBuilder<StationDbContext>().UseSqlite($"Data Source={databasePath};Pooling=False").Options;
            await using var database = new StationDbContext(options);
            await database.Database.MigrateAsync();
            var normalizer = new ToolGoodSearchTextNormalizer();
            var search = new SqliteSongSearchIndex(database, normalizer);
            var importer = new CatalogJsonImportService(database, normalizer, search);

            var first = await importer.ImportAsync(indexPath, root);
            Assert.True(first.IsSuccess, first.Error.Code);
            Assert.Equal(3, first.Value.Added);
            Assert.Equal(0, first.Value.Skipped);
            var songs = await database.Songs.Include(x => x.Artists).ThenInclude(x => x.Artist).Include(x => x.MediaFiles).OrderBy(x => x.Title).ToListAsync();
            Assert.Equal(3, songs.Count);
            Assert.Contains(songs, x => x.Title == "夜曲" && x.Artists.Single().Artist.Name == "周杰伦" && x.Year == 2016 && x.MediaFiles.Single().DurationSeconds == 10);
            Assert.Contains(songs, x => x.Title == "倔强" && x.MediaFiles.Single().RelativePath.EndsWith(".mpg", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, (await search.SearchAsync(new SongSearchQuery("zhoujielun", PageSize: 10))).Value.Total);

            var artists = await new EfArtistBrowseService(database).ListAsync(null);
            var jay = Assert.Single(artists, x => x.Name == "周杰伦");
            Assert.Equal(2, jay.SongCount);
            Assert.Equal("华语男歌手", jay.Group);
            Assert.NotNull(jay.ImageUrl);

            var second = await importer.ImportAsync(indexPath, root);
            Assert.True(second.IsSuccess, second.Error.Code);
            Assert.Equal(0, second.Value.Added);
            Assert.Equal(3, second.Value.Skipped);
            Assert.Equal(3, await database.Songs.CountAsync());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
