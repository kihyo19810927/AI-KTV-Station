using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Rooms;

public sealed record RoomAdminDetails(
    Guid Id,
    string JoinCode,
    RoomStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    int MaxQueuedSongsPerGuest);

public interface IRoomRepository
{
    Task<RoomSession?> FindOpenAsync(CancellationToken cancellationToken = default);
    Task<RoomSession?> FindAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<bool> JoinCodeExistsAsync(string joinCode, CancellationToken cancellationToken = default);
    Task AddAsync(RoomSession room, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRoomJoinCodeGenerator
{
    string Create();
}

public sealed class RoomLifecycleService(IRoomRepository repository, IRoomJoinCodeGenerator joinCodeGenerator)
{
    private const int JoinCodeAttempts = 8;

    public async Task<Result<RoomAdminDetails>> CreateAsync(int maxQueuedSongsPerGuest = 100, CancellationToken cancellationToken = default)
    {
        if (maxQueuedSongsPerGuest is < 1 or > 100)
            return Failure("room.invalid_queue_limit", "Guest queue limit must be between 1 and 100.");
        if (await repository.FindOpenAsync(cancellationToken).ConfigureAwait(false) is not null)
            return Failure("room.already_open", "A room is already open.");

        string? joinCode = null;
        for (var attempt = 0; attempt < JoinCodeAttempts; attempt++)
        {
            var candidate = joinCodeGenerator.Create();
            if (!IsValidJoinCode(candidate))
                return Failure("room.invalid_join_code", "The generated room code is invalid.");
            if (!await repository.JoinCodeExistsAsync(candidate, cancellationToken).ConfigureAwait(false))
            {
                joinCode = candidate;
                break;
            }
        }
        if (joinCode is null) return Failure("room.join_code_exhausted", "A unique room code could not be allocated.");

        var room = new RoomSession
        {
            JoinCode = joinCode,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = RoomStatus.Open,
            OpenSlot = 1,
            MaxQueuedSongsPerGuest = maxQueuedSongsPerGuest,
        };
        await repository.AddAsync(room, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<RoomAdminDetails>.Success(Map(room));
    }

    public async Task<Result<RoomAdminDetails>> CloseAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty) return Failure("room.invalid_id", "Room id is required.");
        var room = await repository.FindAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null) return Failure("room.not_found", "Room was not found.");
        if (room.Status == RoomStatus.Closed) return Result<RoomAdminDetails>.Success(Map(room));
        room.Status = RoomStatus.Closed;
        room.OpenSlot = null;
        room.ClosedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<RoomAdminDetails>.Success(Map(room));
    }

    public async Task<Result<RoomAdminDetails?>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var room = await repository.FindOpenAsync(cancellationToken).ConfigureAwait(false);
        return Result<RoomAdminDetails?>.Success(room is null ? null : Map(room));
    }

    public async Task<Result<RoomAdminDetails>> SetQueueLimitAsync(Guid roomId, int maxQueuedSongsPerGuest, CancellationToken cancellationToken = default)
    {
        if (maxQueuedSongsPerGuest is < 1 or > 100) return Failure("room.invalid_queue_limit", "Guest queue limit must be between 1 and 100.");
        var room = await repository.FindAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null) return Failure("room.not_found", "Room was not found.");
        if (room.Status != RoomStatus.Open) return Failure("room.closed", "The room is closed.");
        room.MaxQueuedSongsPerGuest = maxQueuedSongsPerGuest;
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<RoomAdminDetails>.Success(Map(room));
    }

    private static bool IsValidJoinCode(string value) => value.Length == 6 && value.All(char.IsAsciiLetterOrDigit);
    private static RoomAdminDetails Map(RoomSession room) =>
        new(room.Id, room.JoinCode, room.Status, room.CreatedAt, room.ClosedAt, room.MaxQueuedSongsPerGuest);
    private static Result<RoomAdminDetails> Failure(string code, string message) =>
        Result<RoomAdminDetails>.Failure(new Error(code, message));
}
