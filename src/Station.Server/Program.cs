using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Media;
using Station.Application.Metadata;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Infrastructure.Media;
using Station.Infrastructure.Metadata;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;
using Station.Server.Scanning;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOptions<StationOptions>()
    .BindConfiguration(StationOptions.SectionName)
    .Validate(options => StationOptionsValidator.Validate(options).IsSuccess, "Station configuration is invalid.")
    .ValidateOnStart();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var stationOptions = builder.Configuration.GetSection(StationOptions.SectionName).Get<StationOptions>() ?? new StationOptions();
var dataDirectory = Path.GetFullPath(stationOptions.Storage.DataDirectory, builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "station.db");
builder.Services.AddDbContext<StationDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IMediaScanRepository, EfMediaScanRepository>();
builder.Services.AddScoped<IScanRunReader, EfScanRunReader>();
builder.Services.AddScoped<IMediaFileEnumerator, FileSystemMediaFileEnumerator>();
builder.Services.AddScoped<IMediaFilenameParser, KtvFilenameParser>();
builder.Services.AddScoped<INfoMetadataReader, NfoXmlMetadataReader>();
builder.Services.AddSingleton<ISearchTextNormalizer, ToolGoodSearchTextNormalizer>();
builder.Services.AddScoped<IMediaProbe>(_ => new FfprobeMediaProbe(FindExecutable("ffprobe.exe") ?? "ffprobe.exe", TimeSpan.FromSeconds(30)));
builder.Services.AddScoped<IMediaScanRunner, MediaScanService>();
builder.Services.AddScoped<ISongSearchIndex, SqliteSongSearchIndex>();
builder.Services.AddSingleton<IScanCoordinator, ScanCoordinator>();
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<StationDbContext>().Database.MigrateAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/scans", (StartScanRequest request, IScanCoordinator coordinator) =>
{
    var result = coordinator.Start(request.MediaSourceId);
    return result.IsSuccess
        ? Results.Accepted($"/api/scans/{result.Value.ScanRunId}", result.Value)
        : ScanError(result.Error);
});
app.MapGet("/api/scans/{scanRunId:guid}", async (Guid scanRunId, IScanCoordinator coordinator, IScanRunReader reader, CancellationToken cancellationToken) =>
{
    var result = await GetScanAsync(scanRunId, coordinator, reader, cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : ScanError(result.Error);
});
app.MapGet("/api/scans/{scanRunId:guid}/result", async (Guid scanRunId, IScanCoordinator coordinator, IScanRunReader reader, CancellationToken cancellationToken) =>
{
    var result = await GetScanAsync(scanRunId, coordinator, reader, cancellationToken);
    if (!result.IsSuccess) return ScanError(result.Error);
    return result.Value.Status is Station.Domain.Models.ScanStatus.Pending or Station.Domain.Models.ScanStatus.Running
        ? Results.Accepted($"/api/scans/{scanRunId}/result", result.Value)
        : Results.Ok(result.Value);
});
app.MapPost("/api/scans/{scanRunId:guid}/cancel", (Guid scanRunId, IScanCoordinator coordinator) =>
{
    var result = coordinator.Cancel(scanRunId);
    return result.IsSuccess ? Results.Accepted($"/api/scans/{scanRunId}", result.Value) : ScanError(result.Error);
});
app.Run();

static IResult ScanError(Station.Application.Common.Error error)
{
    var status = error.Code switch
    {
        "scan.operation_not_found" => StatusCodes.Status404NotFound,
        "scan.already_running" or "scan.operation_finished" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
    return Results.Problem(statusCode: status, title: error.Message, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}

static async Task<Station.Application.Common.Result<ScanOperationStatus>> GetScanAsync(
    Guid scanRunId,
    IScanCoordinator coordinator,
    IScanRunReader reader,
    CancellationToken cancellationToken)
{
    var current = coordinator.Get(scanRunId);
    if (current.IsSuccess) return current;
    var stored = await reader.FindAsync(scanRunId, cancellationToken);
    if (stored is null) return current;
    return Station.Application.Common.Result<ScanOperationStatus>.Success(new ScanOperationStatus(
        stored.Id,
        stored.MediaSourceId ?? Guid.Empty,
        stored.Status,
        stored.CreatedAt,
        stored.CompletedAt,
        stored.DiscoveredFiles,
        stored.UpdatedFiles,
        stored.ErrorCount,
        stored.ErrorSummary));
}

static string? FindExecutable(string name)
{
    foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var candidate = Path.Combine(directory, name);
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

public partial class Program;

internal sealed record StartScanRequest(Guid MediaSourceId);
