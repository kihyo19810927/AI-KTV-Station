using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.Rooms;

public enum RoomRole { Guest, Host }

public enum RoomPermission
{
    ViewCatalog,
    ViewQueue,
    RequestSong,
    RemoveOwnRequest,
    ControlPlayback,
    ReorderQueue,
    RemoveAnyRequest,
    ManageRoom,
}

public sealed record IssuedRoomToken(
    string Token,
    Guid RoomId,
    Guid GuestId,
    string Nickname,
    RoomRole Role,
    DateTimeOffset ExpiresAt);

public sealed record RoomIdentity(
    Guid RoomId,
    Guid GuestId,
    string Nickname,
    RoomRole Role,
    DateTimeOffset ExpiresAt);

public sealed record RoomGuestAdminDetails(Guid Id, string Nickname, RoomRole Role, DateTimeOffset JoinedAt, DateTimeOffset ExpiresAt, bool IsRevoked);

public sealed record ProtectedRoomToken(string Value, string Hash);

public interface IRoomTokenProtector
{
    ProtectedRoomToken Create();
    string Hash(string token);
}

public interface IRoomIdentityRepository
{
    Task<RoomSession?> FindOpenByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task<RoomSession?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Guest?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guest>> ListGuestsAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task AddGuestAsync(Guest guest, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class RoomAuthorizationPolicy
{
    public static bool Allows(RoomRole role, RoomPermission permission) => role == RoomRole.Host || permission is
        RoomPermission.ViewCatalog or
        RoomPermission.ViewQueue or
        RoomPermission.RequestSong or
        RoomPermission.RemoveOwnRequest or
        RoomPermission.ControlPlayback;
}

public sealed class RoomAuthenticationService(
    IRoomIdentityRepository repository,
    IRoomTokenProtector tokens,
    TimeProvider clock)
{
    private static readonly TimeSpan GuestLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan HostLifetime = TimeSpan.FromHours(8);

    public async Task<Result<IssuedRoomToken>> JoinAsync(
        string joinCode,
        string nickname,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedCode.Length != 6 || !normalizedCode.All(char.IsAsciiLetterOrDigit))
            return Failure<IssuedRoomToken>("auth.invalid_join_code", "A six-character room code is required.");
        var room = await repository.FindOpenByJoinCodeAsync(normalizedCode, cancellationToken).ConfigureAwait(false);
        if (room is null) return Failure<IssuedRoomToken>("auth.room_unavailable", "The room is not open.");
        var normalizedNickname = NormalizeNickname(nickname);
        if (normalizedNickname is null)
        {
            var guests = await repository.ListGuestsAsync(room.Id, cancellationToken).ConfigureAwait(false);
            var used = guests.Select(x => x.Nickname).ToHashSet(StringComparer.Ordinal);
            normalizedNickname = Enumerable.Range(1, guests.Count + 2).Select(x => $"访客{x}").First(x => !used.Contains(x));
        }
        return await IssueAsync(room, normalizedNickname, RoomRole.Guest, GuestLifetime, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssuedRoomToken>> IssueHostAsync(
        Guid roomId,
        string nickname,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty) return Failure<IssuedRoomToken>("auth.invalid_room", "Room id is required.");
        var normalizedNickname = NormalizeNickname(nickname);
        if (normalizedNickname is null)
            return Failure<IssuedRoomToken>("auth.invalid_nickname", "Nickname must contain 1 to 40 characters.");
        var room = await repository.FindRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null || room.Status != RoomStatus.Open)
            return Failure<IssuedRoomToken>("auth.room_unavailable", "The room is not open.");
        return await IssueAsync(room, normalizedNickname, RoomRole.Host, HostLifetime, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<RoomIdentity>> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Failure<RoomIdentity>("auth.token_required", "A room token is required.");
        var guest = await repository.FindByTokenHashAsync(tokens.Hash(token), cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (guest is null) return Failure<RoomIdentity>("auth.token_invalid", "The room token is invalid.");
        if (guest.RevokedAt is not null) return Failure<RoomIdentity>("auth.token_revoked", "The room token was revoked.");
        if (guest.ExpiresAt <= now) return Failure<RoomIdentity>("auth.token_expired", "The room token expired.");
        if (guest.RoomSession.Status != RoomStatus.Open)
            return Failure<RoomIdentity>("auth.room_closed", "The room is closed.");
        return Result<RoomIdentity>.Success(Map(guest));
    }

    public async Task<Result<bool>> RevokeAsync(Guid guestId, CancellationToken cancellationToken = default)
    {
        if (guestId == Guid.Empty) return Failure<bool>("auth.invalid_guest", "Guest id is required.");
        var guest = await repository.FindGuestAsync(guestId, cancellationToken).ConfigureAwait(false);
        if (guest is null) return Failure<bool>("auth.guest_not_found", "Guest was not found.");
        if (guest.RevokedAt is null)
        {
            guest.RevokedAt = clock.GetUtcNow();
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Result<bool>.Success(true);
    }

    public async Task<Result<IReadOnlyList<RoomGuestAdminDetails>>> ListGuestsAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty) return Failure<IReadOnlyList<RoomGuestAdminDetails>>("auth.invalid_room", "Room id is required.");
        var room = await repository.FindRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (room is null) return Failure<IReadOnlyList<RoomGuestAdminDetails>>("room.not_found", "Room was not found.");
        var guests = await repository.ListGuestsAsync(roomId, cancellationToken).ConfigureAwait(false);
        return Result<IReadOnlyList<RoomGuestAdminDetails>>.Success(guests.OrderBy(x => x.JoinedAt).Select(x => new RoomGuestAdminDetails(x.Id, x.Nickname, x.IsHost ? RoomRole.Host : RoomRole.Guest, x.JoinedAt, x.ExpiresAt, x.RevokedAt is not null)).ToArray());
    }

    private async Task<Result<IssuedRoomToken>> IssueAsync(
        RoomSession room,
        string nickname,
        RoomRole role,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var protectedToken = tokens.Create();
        var now = clock.GetUtcNow();
        var guest = new Guest
        {
            RoomSessionId = room.Id,
            Nickname = nickname,
            TokenHash = protectedToken.Hash,
            JoinedAt = now,
            ExpiresAt = now.Add(lifetime),
            IsHost = role == RoomRole.Host,
        };
        await repository.AddGuestAsync(guest, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<IssuedRoomToken>.Success(new(
            protectedToken.Value, room.Id, guest.Id, nickname, role, guest.ExpiresAt));
    }

    private static string? NormalizeNickname(string? nickname)
    {
        var value = nickname?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > 40 ? null : value;
    }

    private static RoomIdentity Map(Guest guest) => new(
        guest.RoomSessionId,
        guest.Id,
        guest.Nickname,
        guest.IsHost ? RoomRole.Host : RoomRole.Guest,
        guest.ExpiresAt);

    private static Result<T> Failure<T>(string code, string message) =>
        Result<T>.Failure(new Error(code, message));
}
