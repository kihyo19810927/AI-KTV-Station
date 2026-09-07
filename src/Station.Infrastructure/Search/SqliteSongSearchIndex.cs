using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Search;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Search;

public sealed class SqliteSongSearchIndex(StationDbContext database, ISearchTextNormalizer normalizer) : ISongSearchIndex
{
    private const int RebuildBatchSize = 1000;

    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        await database.Database.ExecuteSqlRawAsync("DELETE FROM SongSearchDocuments", cancellationToken);
        var total = await database.Songs.CountAsync(cancellationToken);
        for (var offset = 0; offset < total; offset += RebuildBatchSize)
        {
            var ids = await database.Songs.AsNoTracking().OrderBy(x => x.Id).Skip(offset).Take(RebuildBatchSize)
                .Select(x => x.Id).ToArrayAsync(cancellationToken);
            await UpsertAsync(ids, cancellationToken);
        }
    }

    public async Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default)
    {
        if (songIds.Count == 0) return;
        var songs = await database.Songs.AsNoTracking()
            .Include(x => x.Artists).ThenInclude(x => x.Artist)
            .Where(x => songIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var found = songs.Select(x => x.Id).ToHashSet();
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await database.Database.GetDbConnection().BeginTransactionAsync(cancellationToken);
            foreach (var missing in songIds.Where(x => !found.Contains(x)))
                await DeleteAsync(missing, transaction, cancellationToken);
            foreach (var song in songs)
                await UpsertAsync(song, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    public async Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default)
    {
        var validation = Validate(query);
        if (validation is not null) return Result<SongSearchPage>.Failure(validation);
        var match = CompileMatch(query.Text);
        var where = new List<string>();
        if (match is not null) where.Add("SongSearchFts MATCH @match");
        if (!string.IsNullOrWhiteSpace(query.Language)) where.Add("d.Language = @language COLLATE NOCASE");
        if (!string.IsNullOrWhiteSpace(query.Category)) where.Add("d.Category = @category COLLATE NOCASE");
        if (!string.IsNullOrWhiteSpace(query.Quality)) where.Add("d.Quality = @quality COLLATE NOCASE");
        if (query.YearFrom is not null) where.Add("d.Year >= @yearFrom");
        if (query.YearTo is not null) where.Add("d.Year <= @yearTo");
        var from = match is null
            ? "SongSearchDocuments d"
            : "SongSearchFts JOIN SongSearchDocuments d ON d.rowid = SongSearchFts.rowid";
        var predicate = where.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", where)}";
        var rank = match is null ? "0.0" : "bm25(SongSearchFts)";
        var order = query.Sort switch
        {
            SongSearchSort.Title => "d.NormalizedTitle, d.SongId",
            SongSearchSort.YearDescending => "d.Year DESC, d.NormalizedTitle, d.SongId",
            _ when match is not null => "Rank, d.NormalizedTitle, d.SongId",
            _ => "d.NormalizedTitle, d.SongId",
        };

        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = database.Database.GetDbConnection();
            var total = await CountAsync(connection, from, predicate, query, match, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT d.SongId, d.Title, d.Artists, d.Language, d.Category, d.Quality, d.Year, d.Availability, {rank} AS Rank FROM {from}{predicate} ORDER BY {order} LIMIT @limit OFFSET @offset";
            AddParameters(command, query, match);
            Add(command, "@limit", query.PageSize);
            Add(command, "@offset", (long)(query.Page - 1) * query.PageSize);
            var items = new List<SongSearchItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) items.Add(ReadItem(reader));
            return Result<SongSearchPage>.Success(new SongSearchPage(items, total, query.Page, query.PageSize));
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }
    }

    private async Task UpsertAsync(Song song, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var artists = song.Artists.OrderBy(x => x.Order).Select(x => x.Artist).ToArray();
        var artistDisplay = string.Join(" / ", artists.Select(x => x.Name));
        var terms = string.Join('\n', new[]
        {
            song.Title, song.NormalizedTitle, song.SimplifiedTitle, song.TraditionalTitle, song.TitlePinyin, song.TitleInitials, song.CompactTitle,
        }.Concat(artists.SelectMany(ArtistTerms)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO SongSearchDocuments (SongId, Title, NormalizedTitle, Artists, Language, Category, Quality, Year, Availability, Terms)
            VALUES (@id, @title, @normalizedTitle, @artists, @language, @category, @quality, @year, @availability, @terms)
            ON CONFLICT(SongId) DO UPDATE SET Title=excluded.Title, NormalizedTitle=excluded.NormalizedTitle, Artists=excluded.Artists,
              Language=excluded.Language, Category=excluded.Category, Quality=excluded.Quality, Year=excluded.Year,
              Availability=excluded.Availability, Terms=excluded.Terms
            """;
        Add(command, "@id", song.Id.ToString("D"));
        Add(command, "@title", song.Title);
        Add(command, "@normalizedTitle", song.NormalizedTitle);
        Add(command, "@artists", artistDisplay);
        Add(command, "@language", song.Language);
        Add(command, "@category", song.Category);
        Add(command, "@quality", song.Quality);
        Add(command, "@year", song.Year);
        Add(command, "@availability", song.Availability.ToString());
        Add(command, "@terms", terms);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task DeleteAsync(Guid songId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM SongSearchDocuments WHERE SongId=@id";
        Add(command, "@id", songId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<string> ArtistTerms(Artist artist) =>
        [artist.Name, artist.NormalizedName, artist.SimplifiedName, artist.TraditionalName, artist.Pinyin ?? string.Empty, artist.Initials ?? string.Empty, artist.CompactName];

    private string? CompileMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var keys = normalizer.CreateKeys(text);
        var terms = new[] { keys.Normalized, keys.Simplified, keys.Traditional, keys.Pinyin, keys.Initials, keys.Compact }
            .Where(x => !string.IsNullOrWhiteSpace(x) && SearchTextNormalization.Compact(x).Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(x => $"\"{x.Replace("\"", "\"\"")}\"*").ToArray();
        return terms.Length == 0 ? "\"__no_search_terms__\"" : $"({string.Join(" OR ", terms)})";
    }

    private static Error? Validate(SongSearchQuery query)
    {
        if (query.Page < 1) return new Error("search.invalid_page", "Page must be at least one.");
        if (query.PageSize is < 1 or > 100) return new Error("search.invalid_page_size", "Page size must be between one and one hundred.");
        if (query.Text?.Length > 200) return new Error("search.query_too_long", "Search text must not exceed two hundred characters.");
        if (query.YearFrom is not null && query.YearTo is not null && query.YearFrom > query.YearTo)
            return new Error("search.invalid_year_range", "The starting year must not exceed the ending year.");
        return null;
    }

    private static async Task<long> CountAsync(DbConnection connection, string from, string predicate, SongSearchQuery query, string? match, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {from}{predicate}";
        AddParameters(command, query, match);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameters(DbCommand command, SongSearchQuery query, string? match)
    {
        if (match is not null) Add(command, "@match", match);
        if (!string.IsNullOrWhiteSpace(query.Language)) Add(command, "@language", query.Language.Trim());
        if (!string.IsNullOrWhiteSpace(query.Category)) Add(command, "@category", query.Category.Trim());
        if (!string.IsNullOrWhiteSpace(query.Quality)) Add(command, "@quality", query.Quality.Trim());
        if (query.YearFrom is not null) Add(command, "@yearFrom", query.YearFrom);
        if (query.YearTo is not null) Add(command, "@yearTo", query.YearTo);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static SongSearchItem ReadItem(DbDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Enum.Parse<AvailabilityStatus>(reader.GetString(7)));
}
