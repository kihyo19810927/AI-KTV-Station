using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Station.Application.Search;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Search;

public sealed class EfArtistBrowseService(StationDbContext database, ArtistLexicon? lexicon = null,
    ArtistBrowseCache? cache = null) : IArtistBrowseService
{
    private readonly ArtistLexicon lexicon = lexicon ?? new ArtistLexicon();
    private readonly ArtistBrowseCache cache = cache ?? new ArtistBrowseCache();

    public async Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default)
    {
        var items = await cache.GetAsync(database.Database.GetConnectionString()!, LoadAsync, cancellationToken).ConfigureAwait(false);
        return items.Where(x => string.IsNullOrWhiteSpace(artistGroup) ||
                string.Equals(x.Group, artistGroup, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 5_000)).ToArray();
    }

    private async Task<IReadOnlyList<ArtistBrowseItem>> LoadAsync(CancellationToken token)
    {
        // Only aggregate artist rows cross the database boundary. Categories reuse the cache.
        var rows = await database.Database.SqlQueryRaw<ArtistStatistics>("""
            WITH plays AS (
                SELECT SongId, COUNT(*) AS Total FROM PlayHistory GROUP BY SongId
            )
            SELECT MIN(a.Name) AS Name, COUNT(*) AS SongCount,
                COALESCE(SUM(p.Total), 0) AS PlayCount
            FROM SongArtists sa JOIN Artists a ON a.Id = sa.ArtistId
            LEFT JOIN plays p ON p.SongId = sa.SongId
            GROUP BY a.Name COLLATE NOCASE
            """).ToListAsync(token).ConfigureAwait(false);
        return rows.Select(row =>
        {
            var metadata = lexicon.Resolve(row.Name);
            var score = metadata.Popularity * 10 + row.PlayCount * 5 + row.SongCount;
            return new ArtistBrowseItem(CreateStableId(row.Name), row.Name, row.SongCount, metadata.ImageUrl, metadata.Group, score);
        }).OrderByDescending(x => x.Popularity).ThenByDescending(x => x.SongCount)
            .ThenBy(x => x.Name).ToArray();
    }

    private static Guid CreateStableId(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
    public sealed class ArtistStatistics
    {
        public string Name { get; set; } = "";
        public int SongCount { get; set; }
        public int PlayCount { get; set; }
    }
}
