using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Station.Application.Rooms;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Rooms;

public sealed class EfRoomRepository(StationDbContext database) : IRoomRepository
{
    public Task<RoomSession?> FindOpenAsync(CancellationToken cancellationToken = default) =>
        database.RoomSessions.SingleOrDefaultAsync(x => x.Status == RoomStatus.Open, cancellationToken);

    public Task<RoomSession?> FindAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        database.RoomSessions.SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);

    public Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken cancellationToken = default) =>
        database.RoomSessions.AnyAsync(x => x.JoinCode == joinCode, cancellationToken);

    public Task AddAsync(RoomSession room, CancellationToken cancellationToken = default) =>
        database.RoomSessions.AddAsync(room, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}

public sealed class SecureRoomJoinCodeGenerator : IRoomJoinCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public string Create() => string.Create(6, 0, static (buffer, _) =>
    {
        for (var index = 0; index < buffer.Length; index++)
            buffer[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });
}
