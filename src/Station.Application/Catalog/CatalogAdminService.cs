using Station.Application.Common;
using Station.Application.Search;
using Station.Domain.Models;

namespace Station.Application.Catalog;

public sealed record SongAdminDetails(Guid Id, string Title, string Artists, string? Language, string? Category, int? Year, string? Quality, AvailabilityStatus Availability, int MediaFileCount);
public sealed record SongMetadataUpdate(string Title, string? Language, string? Category, int? Year, string? Quality);

public interface ICatalogAdminRepository
{
    Task<Song?> FindAsync(Guid songId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICatalogAdminService
{
    Task<Result<SongAdminDetails>> GetAsync(Guid songId, CancellationToken cancellationToken = default);
    Task<Result<SongAdminDetails>> UpdateAsync(Guid songId, SongMetadataUpdate update, CancellationToken cancellationToken = default);
}

public sealed class CatalogAdminService(ICatalogAdminRepository repository, ISearchTextNormalizer normalizer, ISongSearchIndex search) : ICatalogAdminService
{
    public async Task<Result<SongAdminDetails>> GetAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        var song = await repository.FindAsync(songId, cancellationToken);
        return song is null ? Failure("catalog.song_not_found", "Song was not found.") : Result<SongAdminDetails>.Success(Map(song));
    }

    public async Task<Result<SongAdminDetails>> UpdateAsync(Guid songId, SongMetadataUpdate update, CancellationToken cancellationToken = default)
    {
        if (update is null) return Failure("catalog.update_required", "Metadata update is required.");
        var title = update.Title?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 300) return Failure("catalog.title_invalid", "Title must contain 1 to 300 characters.");
        if (update.Year is < 1900 or > 2100) return Failure("catalog.year_invalid", "Year must be between 1900 and 2100.");
        var song = await repository.FindAsync(songId, cancellationToken);
        if (song is null) return Failure("catalog.song_not_found", "Song was not found.");
        song.Title = title; song.Language = Clean(update.Language); song.Category = Clean(update.Category); song.Year = update.Year; song.Quality = Clean(update.Quality);
        SongSearchKeyUpdater.Update(song, normalizer);
        await repository.SaveChangesAsync(cancellationToken);
        await search.UpsertAsync([song.Id], cancellationToken);
        return Result<SongAdminDetails>.Success(Map(song));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static SongAdminDetails Map(Song song) => new(song.Id, song.Title, string.Join(" / ", song.Artists.OrderBy(x => x.Order).Select(x => x.Artist.Name)), song.Language, song.Category, song.Year, song.Quality, song.Availability, song.MediaFiles.Count);
    private static Result<SongAdminDetails> Failure(string code, string message) => Result<SongAdminDetails>.Failure(new Error(code, message));
}
