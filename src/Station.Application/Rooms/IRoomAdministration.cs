using Station.Application.Common;

namespace Station.Application.Rooms;

public interface IRoomLifecycleService
{
    Task<Result<RoomAdminDetails>> CreateAsync(int maxQueuedSongsPerGuest = 100, CancellationToken cancellationToken = default);
    Task<Result<RoomAdminDetails>> CloseAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<RoomAdminDetails?>> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<Result<RoomAdminDetails>> SetQueueLimitAsync(Guid roomId, int maxQueuedSongsPerGuest, CancellationToken cancellationToken = default);
}

public interface IRoomHostAdministration
{
    Task<Result<IssuedRoomToken>> IssueHostAsync(Guid roomId, string nickname, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeAsync(Guid guestId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RoomGuestAdminDetails>>> ListGuestsAsync(Guid roomId, CancellationToken cancellationToken = default);
}
