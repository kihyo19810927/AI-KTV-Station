using System.Net;
using Station.Application.Common;
using Station.Application.Library;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Search;
using Station.Server.Realtime;
using Station.Server.Security;

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
        api.MapGet("/catalog/artists", BrowseArtistsAsync).WithName("BrowseArtists");
        api.MapGet("/queue", GetQueueAsync).WithName("GetQueue");
        api.MapPost("/queue", RequestSongAsync).WithName("RequestSong");
        api.MapDelete("/queue/{itemId:guid}", RemoveQueueItemAsync).WithName("RemoveQueueItem");
        api.MapPost("/queue/{itemId:guid}/insert", InsertQueueItemAsync).WithName("InsertQueueItem");
        api.MapPost("/queue/{itemId:guid}/top", MoveQueueItemToTopAsync).WithName("MoveQueueItemToTop");

        api.MapGet("/library/favorites", GetFavoritesAsync).WithName("GetFavorites");
        api.MapPut("/library/favorites/{songId:guid}", SetFavoriteAsync).WithName("SetFavorite");
        api.MapGet("/library/history", GetHistoryAsync).WithName("GetPlaybackHistory");
        api.MapGet("/library/popular", GetPopularAsync).WithName("GetPopularSongs");
        api.MapGet("/profiles", ListProfilesAsync).WithName("ListHouseholdProfiles");
        api.MapPost("/profiles", CreateProfileAsync).WithName("CreateHouseholdProfile");
        api.MapPost("/profiles/{profileId:guid}/activate", ActivateProfileAsync).WithName("ActivateHouseholdProfile");
        api.MapGet("/profiles/session", ResolveProfileAsync).WithName("ResolveHouseholdProfile");
        api.MapGet("/profiles/{profileId:guid}/playlists", ListProfilePlaylistsAsync).WithName("ListProfilePlaylists");
        api.MapGet("/profiles/{profileId:guid}/favorites", ListProfileFavoritesAsync).WithName("ListProfileFavorites");
        api.MapPost("/profiles/{profileId:guid}/playlists", CreateProfilePlaylistAsync).WithName("CreateProfilePlaylist");
        api.MapPut("/profiles/{profileId:guid}/favorites/{songId:guid}", SetProfileFavoriteAsync).WithName("SetProfileFavorite");

        api.MapGet("/playback", GetPlaybackAsync).WithName("GetPlayback");
        api.MapPost("/playback/play", PlayAsync).WithName("ResumePlayback");
        api.MapPost("/playback/pause", PauseAsync).WithName("PausePlayback");
        api.MapPost("/playback/skip", SkipAsync).WithName("SkipPlayback");
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
        if (!LocalRequestPolicy.IsLocal(context.Connection.RemoteIpAddress)) return Problem(new Error("auth.local_only", "Room administration is available only on the host."));
        var created = await rooms.CreateAsync(request.MaxQueuedSongsPerGuest ?? 100, cancellationToken);
        if (created.IsFailure) return Problem(created.Error);
        var host = await authentication.IssueHostAsync(created.Value.Id, request.HostNickname ?? "主持人", cancellationToken);
        if (host.IsFailure) return Problem(host.Error);
        await PublishAsync(context, created.Value.Id, "room.created", created.Value, cancellationToken);
        return Results.Created($"/api/rooms/{created.Value.Id}", new RoomCreatedResponse(created.Value, host.Value));
    }

    private static async Task<IResult> GetCurrentRoomAsync(
        HttpContext context,
        RoomLifecycleService rooms,
        CancellationToken cancellationToken)
    {
        if (!LocalRequestPolicy.IsLocal(context.Connection.RemoteIpAddress)) return Problem(new Error("auth.local_only", "Room administration is available only on the host."));
        var current = await rooms.GetCurrentAsync(cancellationToken);
        return current.IsSuccess ? Results.Ok(current.Value) : Problem(current.Error);
    }

    private static async Task<IResult> JoinRoomAsync(
        HttpContext context,
        JoinRoomRequest request,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var result = await authentication.JoinAsync(request.JoinCode ?? string.Empty, request.Nickname ?? string.Empty, cancellationToken);
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, result.Value.RoomId, "guest.joined", new { result.Value.GuestId, result.Value.Nickname, result.Value.Role }, cancellationToken);
        return Results.Ok(result.Value);
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
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, roomId, "room.closed", result.Value, cancellationToken);
        return Results.Ok(result.Value);
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
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, roomId, "guest.revoked", new { GuestId = guestId }, cancellationToken);
        return Results.NoContent();
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
        string? artistGroup = null,
        string? artist = null,
        string? quality = null,
        int? yearFrom = null,
        int? yearTo = null,
        SongSearchSort sort = SongSearchSort.Relevance,
        SongSearchField field = SongSearchField.Any,
        CancellationToken cancellationToken = default)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await search.SearchAsync(new(text, page, pageSize, language, category, quality, yearFrom, yearTo, sort, artistGroup, artist, field), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> BrowseArtistsAsync(HttpContext context, RoomAuthenticationService authentication,
        IArtistBrowseService artists, string? artistGroup = null, CancellationToken cancellationToken = default)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        return Results.Ok(await artists.ListAsync(artistGroup, cancellationToken: cancellationToken));
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
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, identity.Value.RoomId, "queue.added", result.Value, cancellationToken);
        return Results.Created($"/api/queue/{result.Value.Id}", result.Value);
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
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, identity.Value.RoomId, "queue.removed", new { ItemId = itemId }, cancellationToken);
        return Results.NoContent();
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
        if (result.IsFailure) return Problem(result.Error);
        var ordered = await queue.ListAsync(identity.Value, cancellationToken);
        if (ordered.IsSuccess)
            foreach (var entry in ordered.Value)
                await PublishAsync(context, identity.Value.RoomId, "queue.reordered", entry, cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> InsertQueueItemAsync(
        Guid itemId,
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomQueueService queue,
        CancellationToken cancellationToken)
    {
        var identity = await AuthenticateAsync(context, authentication, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await queue.InsertNextAsync(identity.Value, itemId, cancellationToken);
        if (result.IsFailure) return Problem(result.Error);
        var ordered = await queue.ListAsync(identity.Value, cancellationToken);
        if (ordered.IsSuccess)
            foreach (var entry in ordered.Value)
                await PublishAsync(context, identity.Value.RoomId, "queue.reordered", entry, cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetPlaybackAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        PlaybackControlService playback,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewQueue, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await playback.GetSnapshotAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetFavoritesAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomLibraryService library,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await library.ListFavoritesAsync(identity.Value, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> SetFavoriteAsync(
        Guid songId,
        FavoriteRequest request,
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomLibraryService library,
        CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await library.SetFavoriteAsync(identity.Value, songId, request.Favorite, cancellationToken);
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, identity.Value.RoomId, "favorite.changed", new { SongId = songId, request.Favorite, identity.Value.GuestId }, cancellationToken);
        return Results.Ok(new { SongId = songId, Favorite = result.Value });
    }

    private static async Task<IResult> GetHistoryAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomLibraryService library,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await library.ListHistoryAsync(identity.Value, page, pageSize, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> GetPopularAsync(
        HttpContext context,
        RoomAuthenticationService authentication,
        RoomLibraryService library,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var result = await library.ListPopularAsync(identity.Value, take, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> ListProfilesAsync(HttpContext context, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        return identity.IsFailure ? Problem(identity.Error) : Results.Ok(await profiles.ListProfilesAsync(cancellationToken));
    }

    private static async Task<IResult> CreateProfileAsync(HttpContext context, CreateProfileRequest request, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        try { return Results.Created("/api/profiles", await profiles.CreateAsync(request.DisplayName, request.AvatarUrl, cancellationToken)); }
        catch (ArgumentException exception) { return Problem(new Error("profile.invalid_name", exception.Message)); }
    }

    private static async Task<IResult> ActivateProfileAsync(Guid profileId, HttpContext context, ActivateProfileRequest request, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var session = await profiles.ActivateAsync(profileId, request.Pin, cancellationToken);
        return session is null ? Problem(new Error("profile.activation_denied", "Profile was not found or its PIN is incorrect.")) : Results.Ok(session);
    }

    private static async Task<IResult> ResolveProfileAsync(HttpContext context, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var session = await profiles.ResolveAsync(context.Request.Headers["X-Station-Profile-Token"].ToString(), cancellationToken);
        return session is null ? Results.NoContent() : Results.Ok(session.Profile);
    }

    private static async Task<IResult> ListProfilePlaylistsAsync(Guid profileId, HttpContext context, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        return Results.Ok(await profiles.ListPlaylistsAsync(profileId, cancellationToken));
    }

    private static async Task<IResult> CreateProfilePlaylistAsync(Guid profileId, HttpContext context, CreatePlaylistRequest request, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var session = await profiles.ResolveAsync(context.Request.Headers["X-Station-Profile-Token"].ToString(), cancellationToken);
        if (session?.Profile.Id != profileId) return Problem(new Error("profile.forbidden", "Activate this profile on this device before changing its playlists."));
        var result = await profiles.CreatePlaylistAsync(profileId, request.Name, request.IsFamilyShared, cancellationToken);
        return result.IsSuccess ? Results.Created($"/api/profiles/{profileId}/playlists/{result.Value.Id}", result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> ListProfileFavoritesAsync(Guid profileId, HttpContext context, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        return identity.IsFailure ? Problem(identity.Error) : Results.Ok(await profiles.ListFavoritesAsync(profileId, cancellationToken));
    }

    private static async Task<IResult> SetProfileFavoriteAsync(Guid profileId, Guid songId, HttpContext context, FavoriteRequest request, RoomAuthenticationService authentication, ProfileLibraryService profiles, CancellationToken cancellationToken)
    {
        var identity = await AuthorizeAsync(context, authentication, RoomPermission.ViewCatalog, cancellationToken);
        if (identity.IsFailure) return Problem(identity.Error);
        var session = await profiles.ResolveAsync(context.Request.Headers["X-Station-Profile-Token"].ToString(), cancellationToken);
        if (session?.Profile.Id != profileId) return Problem(new Error("profile.forbidden", "Activate this profile on this device before changing its favorites."));
        var result = await profiles.SetFavoriteAsync(profileId, songId, request.Favorite, cancellationToken);
        return result.IsSuccess ? Results.Ok(new { result.Value }) : Problem(result.Error);
    }

    private static Task<IResult> PlayAsync(HttpContext context, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, playback.PlayAsync);
    private static Task<IResult> PauseAsync(HttpContext context, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, playback.PauseAsync);
    private static Task<IResult> SkipAsync(HttpContext context, RoomAuthenticationService auth, PlaybackControlService playback, CancellationToken token) =>
        HostControlAsync(context, auth, token, playback.SkipAsync);
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
        if (result.IsFailure) return Problem(result.Error);
        await PublishAsync(context, identity.Value.RoomId, "playback.changed", result.Value, cancellationToken);
        return Results.Ok(result.Value);
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

    private static Task<RoomRealtimeEvent> PublishAsync(
        HttpContext context,
        Guid roomId,
        string type,
        object payload,
        CancellationToken cancellationToken) =>
        context.RequestServices.GetRequiredService<IRoomRealtimePublisher>()
            .PublishAsync(roomId, type, payload, cancellationToken);

    public static IResult Problem(Error error)
    {
        var status = error.Code switch
        {
            "auth.token_required" or "auth.token_invalid" or "auth.token_expired" or "auth.token_revoked" or "auth.room_closed" => StatusCodes.Status401Unauthorized,
            "auth.forbidden" or "auth.local_only" or "auth.room_mismatch" or "queue.forbidden" or "profile.forbidden" => StatusCodes.Status403Forbidden,
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
public sealed record FavoriteRequest(bool Favorite);
public sealed record CreateProfileRequest(string DisplayName, string? AvatarUrl);
public sealed record ActivateProfileRequest(string? Pin);
public sealed record CreatePlaylistRequest(string Name, bool IsFamilyShared);
