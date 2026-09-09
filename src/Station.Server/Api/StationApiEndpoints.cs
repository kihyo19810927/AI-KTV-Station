using System.Net;
using Station.Application.Common;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Search;

namespace Station.Server.Api;

public static class StationApiEndpoints
{
    public static IEndpointRouteBuilder MapStationApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapPost("/rooms", CreateRoomAsync).WithName("CreateRoom");
        api.MapGet("/rooms/current", GetCurrentRoomAsync).WithName("GetCurrentRoom");
        api.MapPost("/rooms/join", JoinRoomAsync).WithName("JoinRoom");
        api.MapPost("/rooms/{roomId:guid}/close", CloseRoomAsync).WithName("CloseRoom");
        api.MapPost("/rooms/{roomId:guid}/guests/{guestId:guid}/revoke", RevokeGuestAsync).WithName("RevokeGuest");

        api.MapGet("/catalog/search", SearchAsync).WithName("SearchCatalog");
        api.MapGet("/queue", GetQueueAsync).WithName("GetQueue");
        api.MapPost("/queue", RequestSongAsync).WithName("RequestSong");
        api.MapDelete("/queue/{itemId:guid}", RemoveQueueItemAsync).WithName("RemoveQueueItem");
        api.MapPost("/queue/{itemId:guid}/top", MoveQueueItemToTopAsync).WithName("MoveQueueItemToTop");

        api.MapGet("/playback", GetPlaybackAsync).WithName("GetPlayback");
        api.MapPost("/playback/play", PlayAsync).WithName("ResumePlayback");
        api.MapPost("/playback/pause", PauseAsync).WithName("PausePlayback");
        api.MapPost("/playback/volume", SetVolumeAsync).WithName("SetPlaybackVolume");
        api.MapPost("/playback/seek", SeekAsync).WithName("SeekPlayback");
        api.MapPost("/playback/audio", SelectAudioAsync).WithName("SelectAudioTrack");
        api.MapPost("/playback/subtitle", SelectSubtitleAsync).WithName("SelectSubtitleTrack");
        return endpoints;
    }

    private static async Task<IResult> CreateRoomAsync(
        HttpContext context,
        CreateRoomRequest request,
        RoomLifecycleService rooms,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return Problem(new Error("auth.local_only", "Room administration is available only on the host."));
        var created = await rooms.CreateAsync(request.MaxQueuedSongsPerGuest ?? 10, cancellationToken);
        if (created.IsFailure) return Problem(created.Error);
        var host = await authentication.IssueHostAsync(created.Value.Id, request.HostNickname ?? "主持人", cancellationToken);
        return host.IsSuccess ? Results.Created($"/api/rooms/{created.Value.Id}", new RoomCreatedResponse(created.Value, host.Value)) : Problem(host.Error);
    }

    private static async Task<IResult> GetCurrentRoomAsync(
        HttpContext context,
        RoomLifecycleService rooms,
        CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return Problem(new Error("auth.local_only", "Room administration is available only on the host."));
        var current = await rooms.GetCurrentAsync(cancellationToken);
        return current.IsSuccess ? Results.Ok(current.Value) : Problem(current.Error);
    }

    private static async Task<IResult> JoinRoomAsync(
        JoinRoomRequest request,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var result = await authentication.JoinAsync(request.JoinCode ?? string.Empty, request.Nickname ?? string.Empty, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> CloseRoomAsync(
        Guid roomId,
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomLifecycleService rooms,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ManageRoom, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        if (identity.Value.RoomId != roomId) return Problem(new Error("auth.room_mismatch", "Token is scoped to another room."));
        var result = await rooms.CloseAsync(roomId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> RevokeGuestAsync(
        Guid roomId,
        Guid guestId,
        HttpContext context,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ManageRoom, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        if (identity.Value.RoomId != roomId) return Problem(new Error("auth.room_mismatch", "Token is scoped to another room."));
        var result = await authentication.RevokeAsync(guestId, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
    }

    private static async Task<IResult> SearchAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        ISongSearchIndex search,
        string? text,
        int page = 1,
        int pageSize = 20,
        string? language = null,
        string? category = null,
        string? quality = null,
        int? yearFrom = null,
        int? yearTo = null,
        SongSearchSort sort = SongSearchSort.Relevance,
        CancellationToken cancellationToken = default)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await search.SearchAsync(new(text, page, pageSize, language, category, quality, yearFrom, yearTo, sort), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetQueueAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomQueueService queue,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewQueue, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await queue.ListAsync(identity.Value, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> RequestSongAsync(
        HttpContext context,
        QueueSongRequest request,
        RoomAuthenticationService authentication,
        RoomQueueService queue,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.RequestSong, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await queue.RequestAsync(identity.Value, request.SongId, cancellationToken);
        return result.IsSuccess ? Results.Created($"/api/queue/{result.Value.Id}", result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> RemoveQueueItemAsync(
        Guid itemId,
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomQueueService queue,
        CancellationToken cancellationToken)
    {
        var identity = await AuthenticateAsync(context, authentication, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await queue.RemoveAsync(identity.Value, itemId, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
    }

    private static async Task<IResult> MoveQueueItemToTopAsync(
        Guid itemId,
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomQueueService queue,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ReorderQueue, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await queue.MoveToTopAsync(identity.Value, itemId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetPlaybackAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        PlaybackControlService playback,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewQueue, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await playback.GetProgressAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static Task<IResult> PlayAsync(HttpContext context, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, playback.PlayAsync);
    private static Task<IResult> PauseAsync(HttpContext context, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, playback.PauseAsync);
    private static Task<IResult> SetVolumeAsync(HttpContext context, VolumeRequest request, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, ct => playback.SetVolumeAsync(request.Volume, ct));
    private static Task<IResult> SeekAsync(HttpContext context, SeekRequest request, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, ct => playback.SeekAsync(TimeSpan.FromSeconds(request.PositionSeconds), ct));
    private static Task<IResult> SelectAudioAsync(HttpContext context, AudioTrackRequest request, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, ct => playback.SelectAudioAsync(request.StreamId, ct));
    private static Task<IResult> SelectSubtitleAsync(HttpContext context, SubtitleTrackRequest request, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, ct => playback.SelectSubtitleAsync(request.StreamId, ct));

    private static async Task<IResult> HostControlAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<Result<PlayerSnapshot>>> action)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ControlPlayback, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await action(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<Result<RoomIdentity>> AuthorizeAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomPermission permission,
        CancellationToken cancellationToken)
    {
        var identity = await AuthenticateAsync(context, authentication, cancellationToken);
        if (identity.IsFailure) return identity;
        return RoomAuthorizationPolicy.Allows(identity.Value.Role, permission)
            ? identity
            : Result<RoomIdentity>.Failure(new Error("auth.forbidden", "This room role cannot perform the operation."));
    }

    private static Task<Result<RoomIdentity>> AuthenticateAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : string.Empty;
        return authentication.ValidateAsync(token, cancellationToken);
    }

    private static bool IsLocal(HttpContext context) =>
        context.Connection.RemoteIpAddress is null || IPAddress.IsLoopback(context.Connection.RemoteIpAddress);

    public static IResult Problem(Error error)
    {
        var status = error.Code switch
        {
            "auth.token_required" or "auth.token_invalid" or "auth.token_expired" or "auth.token_revoked" or "auth.room_closed" => StatusCodes.Status401Unauthorized,
            "auth.forbidden" or "auth.local_only" or "auth.room_mismatch" or "queue.forbidden" => StatusCodes.Status403Forbidden,
            var code when code.EndsWith("not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            "room.already_open" or "scan.already_running" or "scan.operation_finished" or "queue.guest_limit_reached" or "queue.item_not_mutable" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(statusCode: status, title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}

public sealed record CreateRoomRequest(int? MaxQueuedSongsPerGuest, string? HostNickname);
public sealed record RoomCreatedResponse(RoomAdminDetails Room, IssuedRoomToken Host);
public sealed record JoinRoomRequest(string? JoinCode, string? Nickname);
public sealed record QueueSongRequest(Guid SongId);
public sealed record VolumeRequest(double Volume);
public sealed record SeekRequest(double PositionSeconds);
public sealed record AudioTrackRequest(int StreamId);
public sealed record SubtitleTrackRequest(int? StreamId);
