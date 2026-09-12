using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Station.Application.Search;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Search;

public sealed class EfArtistBrowseService(StationDbContext database, ArtistLexicon? lexicon = null) : IArtistBrowseService
{
    private readonly ArtistLexicon lexicon = lexicon ?? new ArtistLexicon();

    public async Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default)
    {
        var songs = await database.SongArtists.AsNoTracking()
            .Select(x => new { x.Artist.Name, x.SongId }).ToListAsync(cancellationToken);
        var playCounts = await database.PlayHistory.AsNoTracking().GroupBy(x => x.SongId)
            .Select(x => new { SongId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.SongId, x => x.Count, cancellationToken);
        var favoriteCounts = await database.Favorites.AsNoTracking().GroupBy(x => x.SongId)
            .Select(x => new { SongId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.SongId, x => x.Count, cancellationToken);

        return songs.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(group =>
            {
                var metadata = lexicon.Resolve(group.Key);
                var plays = group.Sum(x => playCounts.GetValueOrDefault(x.SongId));
                var favorites = group.Sum(x => favoriteCounts.GetValueOrDefault(x.SongId));
                var score = metadata.Popularity * 10 + plays * 5 + favorites * 8 + group.Count();
                return new ArtistBrowseItem(CreateStableId(group.Key), group.Key, group.Count(), metadata.ImageUrl, metadata.Group, score);
            })
            .Where(x => string.IsNullOrWhiteSpace(artistGroup) || string.Equals(x.Group, artistGroup, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Popularity).ThenByDescending(x => x.SongCount).ThenBy(x => x.Name)
            .Take(Math.Clamp(limit, 1, 500)).ToList();
    }

    private static Guid CreateStableId(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
}
