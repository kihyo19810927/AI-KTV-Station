using Microsoft.EntityFrameworkCore;
using Station.Application.Catalog;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Catalog;

public sealed class EfCatalogAdminRepository(StationDbContext database) : ICatalogAdminRepository
{
    public Task<Song?> FindAsync(Guid songId, CancellationToken cancellationToken = default) => database.Songs.Include(x => x.Artists).ThenInclude(x => x.Artist).Include(x => x.MediaFiles).SingleOrDefaultAsync(x => x.Id == songId, cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
