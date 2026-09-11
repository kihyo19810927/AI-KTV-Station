using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Search;

public interface ISongSearchIndex
{
    Task RebuildAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default);
    Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default);
}

public enum SongSearchSort { Relevance, Title, YearDescending, RecentlyAdded }

public sealed record SongSearchQuery(
    string? Text = null,
    int Page = 1,
    int PageSize = 20,
    string? Language = null,
    string? Category = null,
    string? Quality = null,
    int? YearFrom = null,
    int? YearTo = null,
    SongSearchSort Sort = SongSearchSort.Relevance,
    string? ArtistGroup = null,
    string? Artist = null);

public sealed record SongSearchPage(IReadOnlyList<SongSearchItem> Items, long Total, int Page, int PageSize);

public sealed record SongSearchItem(
    Guid SongId,
    string Title,
    string Artists,
    string? Language,
    string? Category,
    string? Quality,
    int? Year,
    AvailabilityStatus Availability,
    DateTimeOffset? AddedAt = null);
