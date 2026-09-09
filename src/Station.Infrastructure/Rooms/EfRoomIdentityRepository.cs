using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Rooms;

public sealed class EfRoomIdentityRepository(StationDbContext database) : IRoomIdentityRepository
{
    public Task<RoomSession?> FindOpenByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default) =>
        database.RoomSessions.SingleOrDefaultAsync(
            x => x.JoinCode == joinCode && x.Status == RoomStatus.Open,
            cancellationToken);

    public Task<RoomSession?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        database.RoomSessions.SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);

    public Task<Guest?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        database.Guests.Include(x => x.RoomSession).SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default) =>
        database.Guests.Include(x => x.RoomSession).SingleOrDefaultAsync(x => x.Id == guestId, cancellationToken);

    public Task AddGuestAsync(Guest guest, CancellationToken cancellationToken = default) =>
        database.Guests.AddAsync(guest, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}

public sealed class Sha256RoomTokenProtector : IRoomTokenProtector
{
    public ProtectedRoomToken Create()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(secret).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new(value, Hash(value));
    }

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
