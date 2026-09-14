using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Application.Rooms;
using Station.Server.Security;
using Station.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace Station.Server.Api;

/// <summary>
/// Host-only management endpoints.  These deliberately never become part of the
/// LAN guest surface: JSON import paths and diagnostic details stay on the Windows host.
/// </summary>
public static class StationManagementEndpoints
{
    public static IEndpointRouteBuilder MapStationManagementApi(this IEndpointRouteBuilder endpoints)
    {
        var management = endpoints.MapGroup("/api/manage");
        management.MapGet("/settings", GetSettingsAsync).WithName("GetStationSettings");
        management.MapPost("/room/ensure", EnsureHostRoomAsync).WithName("EnsureHostRoom");
        management.MapGet("/room/{roomId:guid}/guests", ListRoomGuestsAsync).WithName("ListRoomGuests");
        management.MapGet("/qr", RenderQrAsync).WithName("RenderManagementQr");
        management.MapGet("/catalog/stats", GetCatalogStatsAsync).WithName("GetCatalogStats");
        management.MapPut("/settings", SaveSettingsAsync).WithName("SaveStationSettings");
        management.MapPost("/catalog/import", ImportCatalogAsync).WithName("ImportPortableCatalog");
        management.MapGet("/health", CheckHealthAsync).WithName("CheckStationHealth");
        management.MapGet("/diagnostics/recent", ReadDiagnosticsAsync).WithName("ReadStationDiagnostics");
        management.MapPost("/diagnostics/export", ExportDiagnosticsAsync).WithName("ExportStationDiagnostics");
        return endpoints;
    }

    private static async Task<IResult> EnsureHostRoomAsync(
        HttpContext context,
        LocalHostRoomRequest? request,
        RoomLifecycleService rooms,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        var current = await rooms.GetCurrentAsync(cancellationToken);
        if (current.IsFailure) return StationApiEndpoints.Problem(current.Error);
        var room = current.Value;
        if (room is null)
        {
            var created = await rooms.CreateAsync(request?.MaxQueuedSongsPerGuest ?? 100, cancellationToken);
            if (created.IsFailure)
            {
                // Another local host page may have won the one-room race.
                current = await rooms.GetCurrentAsync(cancellationToken);
                if (current.IsFailure || current.Value is null) return StationApiEndpoints.Problem(created.Error);
                room = current.Value;
            }
            else room = created.Value;
        }
        var host = await authentication.IssueHostAsync(room.Id, request?.HostNickname ?? "主持人", cancellationToken);
        return host.IsSuccess
            ? Results.Ok(new LocalHostRoomResponse(room, host.Value, BuildLanJoinUrl(context, room.JoinCode)))
            : StationApiEndpoints.Problem(host.Error);
    }

    private static string BuildLanJoinUrl(HttpContext context, string joinCode)
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .FirstOrDefault(IsPrivateIpv4)?.ToString() ?? context.Request.Host.Host;
        var port = context.Request.Host.Port is { } requestPort ? $":{requestPort}" : string.Empty;
        return $"{context.Request.Scheme}://{address}{port}/join?code={Uri.EscapeDataString(joinCode)}";
    }

    private static async Task<IResult> ListRoomGuestsAsync(
        HttpContext context,
        Guid roomId,
        RoomAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        var result = await authentication.ListGuestsAsync(roomId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : StationApiEndpoints.Problem(result.Error);
    }

    private static IResult RenderQrAsync(HttpContext context, string? content)
    {
        if (!IsLocal(context)) return LocalOnly();
        if (string.IsNullOrWhiteSpace(content)) return StationApiEndpoints.Problem(new Error("qr.content_required", "QR content is required."));
        using var data = QRCodeGenerator.GenerateQrCode(content.Trim(), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10, [23, 17, 38], [255, 255, 255]);
        return Results.File(png, "image/png");
    }

    private static async Task<IResult> GetCatalogStatsAsync(
        HttpContext context,
        StationDbContext database,
        CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();

        var songCount = await database.Songs.AsNoTracking().LongCountAsync(cancellationToken);
        var artistCount = await database.Artists.AsNoTracking().LongCountAsync(cancellationToken);
        var mediaCount = await database.MediaFiles.AsNoTracking().LongCountAsync(cancellationToken);
        var probedCount = await database.MediaFiles.AsNoTracking()
            .LongCountAsync(file => file.ProbeFingerprint != null || file.DurationSeconds != null, cancellationToken);
        var failedCount = await database.MediaFiles.AsNoTracking()
            .LongCountAsync(file => file.LastErrorCode != null, cancellationToken);

        return Results.Ok(new { songCount, artistCount, mediaCount, probedCount, failedCount });
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
    }

    private static async Task<IResult> GetSettingsAsync(HttpContext context, IStationSettingsStore store, CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        var result = await store.LoadAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : StationApiEndpoints.Problem(result.Error);
    }

    private static async Task<IResult> SaveSettingsAsync(HttpContext context, StationOptions options, IStationSettingsStore store, ILocalDiagnosticLog log, CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        var result = await store.SaveAsync(options, cancellationToken);
        if (result.IsFailure) return StationApiEndpoints.Problem(result.Error);
        await log.WriteAsync("Information", "settings.saved", "Local settings were saved; restart is required for active server settings.", cancellationToken);
        return Results.Accepted("/api/manage/settings", new { restartRequired = true });
    }

    private static async Task<IResult> ImportCatalogAsync(HttpContext context, LocalCatalogImportRequest request, ICatalogJsonImportService importer, ILocalDiagnosticLog log, CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        if (request is null) return StationApiEndpoints.Problem(new Error("catalog.import_required", "Import details are required."));
        var result = await importer.ImportAsync(request.IndexPath ?? string.Empty, request.MountRoot ?? string.Empty, cancellationToken: cancellationToken);
        if (result.IsFailure) return StationApiEndpoints.Problem(result.Error);
        await log.WriteAsync("Information", "catalog.imported", $"Portable catalog import completed: added={result.Value.Added}; skipped={result.Value.Skipped}; errors={result.Value.Errors}.", cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CheckHealthAsync(HttpContext context, IStationHealthService health, CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        return Results.Ok(await health.CheckAsync(cancellationToken));
    }

    private static async Task<IResult> ReadDiagnosticsAsync(HttpContext context, ILocalDiagnosticLog log, int count = 100, CancellationToken cancellationToken = default)
    {
        if (!IsLocal(context)) return LocalOnly();
        var bounded = Math.Clamp(count, 1, 200);
        return Results.Ok(await log.ReadRecentAsync(bounded, cancellationToken));
    }

    private static async Task<IResult> ExportDiagnosticsAsync(HttpContext context, IDiagnosticExportService diagnostics, IStationSettingsStore store, CancellationToken cancellationToken)
    {
        if (!IsLocal(context)) return LocalOnly();
        var settings = await store.LoadAsync(cancellationToken);
        if (settings.IsFailure) return StationApiEndpoints.Problem(settings.Error);
        var result = await diagnostics.ExportAsync(settings.Value, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : StationApiEndpoints.Problem(result.Error);
    }

    private static bool IsLocal(HttpContext context) => LocalRequestPolicy.IsLocal(context.Connection.RemoteIpAddress);
    private static IResult LocalOnly() => StationApiEndpoints.Problem(new Error("auth.local_only", "Station management is available only on the host."));

    public sealed record LocalCatalogImportRequest(string? IndexPath, string? MountRoot);
    public sealed record LocalHostRoomRequest(string? HostNickname, int? MaxQueuedSongsPerGuest);
    public sealed record LocalHostRoomResponse(RoomAdminDetails Room, IssuedRoomToken Host, string JoinUrl);
}
