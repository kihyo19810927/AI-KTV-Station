using Station.Application.Common;
using Station.Application.Rooms;
using Station.Domain.Models;

namespace Station.Application.Library;

public sealed record FavoriteSong(Guid SongId, string Title, string Artists, DateTimeOffset FavoritedAt);
public sealed record PlaybackHistoryEntry(Guid Id, Guid SongId, string Title, PlaybackOutcome Outcome, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
public sealed record PopularSong(Guid SongId, string Title, string Artists, int PlayCount, DateTimeOffset LastPlayedAt);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);

public interface IRoomLibraryRepository
{
    Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default);
    Task<bool> SongExistsAsync(Guid songId, CancellationToken cancellationToken = default);
    Task<Favorite?> FindFavoriteAsync(Guid guestId, Guid songId, CancellationToken cancellationToken = default);
    Task AddFavoriteAsync(Favorite favorite, CancellationToken cancellationToken = default);
    void RemoveFavorite(Favorite favorite);
    Task<IReadOnlyList<FavoriteSong>> ListFavoritesAsync(Guid guestId, CancellationToken cancellationToken = default);
    Task<PagedResult<PlaybackHistoryEntry>> ListHistoryAsync(Guid roomId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PopularSong>> ListPopularAsync(Guid roomId, int take, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class RoomLibraryService(IRoomLibraryRepository repository, TimeProvider clock)
{
    public async Task<Result<bool>> SetFavoriteAsync(RoomIdentity identity, Guid songId, bool favorite, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        if (validation is not null) return Result<bool>.Failure(validation);
        if (songId == Guid.Empty || !await repository.SongExistsAsync(songId, cancellationToken).ConfigureAwait(false))
            return Failure<bool>("library.song_not_found", "Song was not found.");
        var existing = await repository.FindFavoriteAsync(identity.GuestId, songId, cancellationToken).ConfigureAwait(false);
        if (favorite && existing is null)
            await repository.AddFavoriteAsync(new Favorite { GuestId = identity.GuestId, SongId = songId, CreatedAt = clock.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        else if (!favorite && existing is not null)
            repository.RemoveFavorite(existing);
        else
            return Result<bool>.Success(favorite);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<bool>.Success(favorite);
    }

    public async Task<Result<IReadOnlyList<FavoriteSong>>> ListFavoritesAsync(RoomIdentity identity, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        return validation is null
            ? Result<IReadOnlyList<FavoriteSong>>.Success(await repository.ListFavoritesAsync(identity.GuestId, cancellationToken).ConfigureAwait(false))
            : Result<IReadOnlyList<FavoriteSong>>.Failure(validation);
    }

    public async Task<Result<PagedResult<PlaybackHistoryEntry>>> ListHistoryAsync(RoomIdentity identity, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var paging = ValidatePaging(page, pageSize);
        if (paging is not null) return Result<PagedResult<PlaybackHistoryEntry>>.Failure(paging);
        var validation = await ValidateIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        return validation is null
            ? Result<PagedResult<PlaybackHistoryEntry>>.Success(await repository.ListHistoryAsync(identity.RoomId, page, pageSize, cancellationToken).ConfigureAwait(false))
            : Result<PagedResult<PlaybackHistoryEntry>>.Failure(validation);
    }

    public async Task<Result<IReadOnlyList<PopularSong>>> ListPopularAsync(RoomIdentity identity, int take = 20, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 100) return Failure<IReadOnlyList<PopularSong>>("library.invalid_take", "Popular result size must be between 1 and 100.");
        var validation = await ValidateIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        return validation is null
            ? Result<IReadOnlyList<PopularSong>>.Success(await repository.ListPopularAsync(identity.RoomId, take, cancellationToken).ConfigureAwait(false))
            : Result<IReadOnlyList<PopularSong>>.Failure(validation);
    }

    private async Task<Error?> ValidateIdentityAsync(RoomIdentity identity, CancellationToken cancellationToken)
    {
        var guest = await repository.FindGuestAsync(identity.GuestId, cancellationToken).ConfigureAwait(false);
        return guest is null || guest.RoomSessionId != identity.RoomId || guest.RevokedAt is not null || guest.ExpiresAt <= clock.GetUtcNow()
            ? new Error("library.identity_invalid", "Room identity is no longer valid.")
            : null;
    }

    private static Error? ValidatePaging(int page, int pageSize) => page < 1 || pageSize is < 1 or > 100
        ? new Error("library.invalid_paging", "Page must be positive and page size must be between 1 and 100.")
        : null;
    private static Result<T> Failure<T>(string code, string message) => Result<T>.Failure(new Error(code, message));
}
