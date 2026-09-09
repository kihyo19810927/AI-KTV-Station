using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using Station.Application.Library;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Library;

public sealed class EfRoomLibraryRepository(StationDbContext database) : IRoomLibraryRepository
{
    public Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default) =>
        database.Guests.SingleOrDefaultAsync(x => x.Id == guestId, cancellationToken);

    public Task<bool> SongExistsAsync(Guid songId, CancellationToken cancellationToken = default) =>
        database.Songs.AnyAsync(x => x.Id == songId, cancellationToken);

    public Task<Favorite?> FindFavoriteAsync(Guid guestId, Guid songId, CancellationToken cancellationToken = default) =>
        database.Favorites.SingleOrDefaultAsync(x => x.GuestId == guestId && x.SongId == songId, cancellationToken);

    public Task AddFavoriteAsync(Favorite favorite, CancellationToken cancellationToken = default) =>
        database.Favorites.AddAsync(favorite, cancellationToken).AsTask();

    public void RemoveFavorite(Favorite favorite) => database.Favorites.Remove(favorite);

    public async Task<IReadOnlyList<FavoriteSong>> ListFavoritesAsync(Guid guestId, CancellationToken cancellationToken = default)
    {
        var rows = await database.Favorites.AsNoTracking().Include(x => x.Song).ThenInclude(x => x.Artists).ThenInclude(x => x.Artist)
            .Where(x => x.GuestId == guestId).ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Song.Title, StringComparer.OrdinalIgnoreCase)
            .Select(x => new FavoriteSong(x.SongId, x.Song.Title, Artists(x.Song), x.CreatedAt)).ToArray();
    }

    public async Task<PagedResult<PlaybackHistoryEntry>> ListHistoryAsync(Guid roomId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var total = await database.PlayHistory.LongCountAsync(x => x.RoomSessionId == roomId, cancellationToken);
        var offset = (page - 1) * pageSize;
        var pageRows = await database.PlayHistory.FromSqlInterpolated(
            $"SELECT * FROM PlayHistory WHERE RoomSessionId = {roomId} ORDER BY StartedAt DESC, Id DESC LIMIT {pageSize} OFFSET {offset}")
            .AsNoTracking().ToArrayAsync(cancellationToken);
        var songIds = pageRows.Select(x => x.SongId).Distinct().ToArray();
        var titles = await database.Songs.AsNoTracking().Where(x => songIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);
        var items = pageRows.Select(x => new PlaybackHistoryEntry(x.Id, x.SongId, titles.GetValueOrDefault(x.SongId, "Unknown song"), x.Outcome, x.StartedAt, x.EndedAt)).ToArray();
        return new(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<PopularSong>> ListPopularAsync(Guid roomId, int take, CancellationToken cancellationToken = default)
    {
        var aggregates = new List<PopularAggregate>();
        var connection = database.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT SongId, COUNT(*) AS PlayCount, MAX(StartedAt) AS LastPlayedAt
                FROM PlayHistory
                WHERE RoomSessionId = $roomId AND Outcome = 'Completed'
                GROUP BY SongId
                ORDER BY PlayCount DESC, LastPlayedAt DESC, SongId
                LIMIT $take
                """;
            var roomParameter = command.CreateParameter(); roomParameter.ParameterName = "$roomId"; roomParameter.DbType = DbType.Guid; roomParameter.Value = roomId; command.Parameters.Add(roomParameter);
            var takeParameter = command.CreateParameter(); takeParameter.ParameterName = "$take"; takeParameter.Value = take; command.Parameters.Add(takeParameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                aggregates.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1), DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        }
        finally { if (shouldClose) await connection.CloseAsync(); }
        var songIds = aggregates.Select(x => x.SongId).ToArray();
        var songs = await database.Songs.AsNoTracking().Include(x => x.Artists).ThenInclude(x => x.Artist)
            .Where(x => songIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        return aggregates.Where(x => songs.ContainsKey(x.SongId))
            .Select(x => new PopularSong(x.SongId, songs[x.SongId].Title, Artists(songs[x.SongId]), x.Count, x.Last)).ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);

    private static string Artists(Song song) => string.Join(" / ", song.Artists.OrderBy(x => x.Order).Select(x => x.Artist.Name));
    private sealed record PopularAggregate(Guid SongId, int Count, DateTimeOffset Last);
}
