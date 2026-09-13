using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Server.Security;

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
        management.MapPut("/settings", SaveSettingsAsync).WithName("SaveStationSettings");
        management.MapPost("/catalog/import", ImportCatalogAsync).WithName("ImportPortableCatalog");
        management.MapGet("/health", CheckHealthAsync).WithName("CheckStationHealth");
        management.MapGet("/diagnostics/recent", ReadDiagnosticsAsync).WithName("ReadStationDiagnostics");
        management.MapPost("/diagnostics/export", ExportDiagnosticsAsync).WithName("ExportStationDiagnostics");
        return endpoints;
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
}
