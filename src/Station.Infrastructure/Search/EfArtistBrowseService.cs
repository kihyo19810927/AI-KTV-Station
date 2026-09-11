using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Station.Application.Search;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Search;

public sealed class EfArtistBrowseService(StationDbContext database) : IArtistBrowseService
{
    public async Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default)
    {
        var query = database.SongArtists.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(artistGroup)) query = query.Where(x => x.Song.ArtistGroup == artistGroup);
        var rows = await query.GroupBy(x => x.Artist.Name)
            .Select(x => new { Name = x.Key, SongCount = x.Count() })
            .OrderByDescending(x => x.SongCount).ThenBy(x => x.Name).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return rows.Select(x => new ArtistBrowseItem(CreateStableId(x.Name), x.Name, x.SongCount, null)).ToList();
    }

    private static Guid CreateStableId(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
}
