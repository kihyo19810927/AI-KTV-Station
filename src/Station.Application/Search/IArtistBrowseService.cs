namespace Station.Application.Search;

public sealed record ArtistBrowseItem(Guid ArtistId, string Name, int SongCount, string? ImageUrl = null);
public interface IArtistBrowseService
{
    Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default);
}
