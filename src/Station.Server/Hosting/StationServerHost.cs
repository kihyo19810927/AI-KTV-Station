using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Library;
using Station.Application.Media;
using Station.Application.Metadata;
using Station.Application.Playback;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Infrastructure.Library;
using Station.Infrastructure.Media;
using Station.Infrastructure.Metadata;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;
using Station.Infrastructure.Queue;
using Station.Infrastructure.Rooms;
using Station.Infrastructure.Scanning;
using Station.Infrastructure.Search;
using Station.Server.Api;
using Station.Server.Realtime;
using Station.Server.Scanning;
using Station.Server.Security;
using Station.Server.Playback;

namespace Station.Server.Hosting;

public static class StationServerHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null, IPlayerAdapter? sharedPlayer = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Services.AddOptions<StationOptions>().BindConfiguration(StationOptions.SectionName).Validate(x => StationOptionsValidator.Validate(x).IsSuccess, "Station configuration is invalid.").ValidateOnStart();
        builder.Services.ConfigureHttpJsonOptions(x => x.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddOpenApi(); builder.Services.AddSignalR();
        var options = builder.Configuration.GetSection(StationOptions.SectionName).Get<StationOptions>() ?? new();
        var validation = StationOptionsValidator.Validate(options); if (validation.IsFailure) throw new InvalidOperationException(validation.Error.Message);
        var address = IPAddress.Parse(options.Server.BindAddress);
        var host = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
        builder.WebHost.UseUrls($"http://{host}:{options.Server.Port}");
        var dataDirectory = Path.GetFullPath(options.Storage.DataDirectory, builder.Environment.ContentRootPath); Directory.CreateDirectory(dataDirectory);
        builder.Services.AddDbContext<StationDbContext>(db => db.UseSqlite($"Data Source={Path.Combine(dataDirectory, "station.db")}"));
        AddServices(builder.Services, options, sharedPlayer);
        var app = builder.Build(); MapPipeline(app); return app;
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken token = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<StationDbContext>().Database.MigrateAsync(token);
        await scope.ServiceProvider.GetRequiredService<PlaybackStartupRecoveryService>().RecoverAsync(token);
    }

    private static void AddServices(IServiceCollection services, StationOptions options, IPlayerAdapter? sharedPlayer)
    {
        services.AddScoped<IMediaScanRepository, EfMediaScanRepository>(); services.AddScoped<IScanRunReader, EfScanRunReader>(); services.AddScoped<IMediaFileEnumerator, FileSystemMediaFileEnumerator>(); services.AddScoped<IMediaFilenameParser, KtvFilenameParser>(); services.AddScoped<INfoMetadataReader, NfoXmlMetadataReader>(); services.AddSingleton<ISearchTextNormalizer, ToolGoodSearchTextNormalizer>(); services.AddScoped<IMediaProbe>(_ => new FfprobeMediaProbe(FindExecutable("ffprobe.exe") ?? "ffprobe.exe", TimeSpan.FromSeconds(30))); services.AddScoped<IMediaScanRunner, MediaScanService>(); services.AddScoped<ISongSearchIndex, SqliteSongSearchIndex>(); services.AddSingleton<IScanCoordinator, ScanCoordinator>();
        services.AddSingleton(TimeProvider.System); services.AddScoped<IRoomRepository, EfRoomRepository>(); services.AddSingleton<IRoomJoinCodeGenerator, SecureRoomJoinCodeGenerator>(); services.AddScoped<RoomLifecycleService>(); services.AddScoped<IRoomIdentityRepository, EfRoomIdentityRepository>(); services.AddSingleton<IRoomTokenProtector, Sha256RoomTokenProtector>(); services.AddScoped<RoomAuthenticationService>(); services.AddScoped<IRoomQueueRepository, EfRoomQueueRepository>(); services.AddSingleton<IRoomQueueLock, InProcessRoomQueueLock>(); services.AddScoped<RoomQueueService>();
        if (sharedPlayer is null) services.AddSingleton<IPlayerAdapter>(_ => new MpvPlayerAdapter(new PlayerOptions { ExecutablePath = string.IsNullOrWhiteSpace(options.Player.ExecutablePath) ? FindMpvExecutable() ?? "mpv.exe" : options.Player.ExecutablePath, CommandTimeoutSeconds = options.Player.CommandTimeoutSeconds })); else services.AddSingleton(sharedPlayer);
        services.AddScoped<PlaybackControlService>(); services.AddScoped<IPlaybackStartupRecoveryStore, EfPlaybackStartupRecoveryStore>(); services.AddScoped<PlaybackStartupRecoveryService>();
        services.AddScoped<IPlaybackQueueStore, EfPlaybackQueueStore>(); services.AddScoped<IPlaybackFailureStore, EfPlaybackFailureStore>(); services.AddSingleton(new PlaybackRecoveryPolicy()); services.AddScoped<PlaybackRecoveryService>(); services.AddScoped<QueuePlaybackOrchestrator>(); services.AddHostedService<RoomPlaybackHostedService>();
        services.AddScoped<IRoomLibraryRepository, EfRoomLibraryRepository>(); services.AddScoped<RoomLibraryService>(); services.AddSingleton<RoomRealtimeJournal>(); services.AddSingleton<IRoomRealtimePublisher, SignalRRoomRealtimePublisher>();
    }

    private static void MapPipeline(WebApplication app)
    {
        app.UseDefaultFiles(); app.Use(async (context, next) => { context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.XFrameOptions = "DENY"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store"; await next(); }); app.UseStaticFiles();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })); app.MapOpenApi(); app.MapStationApi(); app.MapHub<RoomHub>("/hubs/room"); MapScans(app); app.MapFallbackToFile("index.html");
    }

    private static void MapScans(WebApplication app)
    {
        app.MapPost("/api/scans", (HttpContext c, StartScanRequest r, IScanCoordinator x) => { if (!LocalRequestPolicy.IsLocal(c.Connection.RemoteIpAddress)) return LocalOnly(); var z = x.Start(r.MediaSourceId); return z.IsSuccess ? Results.Accepted($"/api/scans/{z.Value.ScanRunId}", z.Value) : StationApiEndpoints.Problem(z.Error); });
        app.MapGet("/api/scans/{id:guid}", async (HttpContext c, Guid id, IScanCoordinator x, IScanRunReader r, CancellationToken t) => { if (!LocalRequestPolicy.IsLocal(c.Connection.RemoteIpAddress)) return LocalOnly(); var z = await GetScanAsync(id, x, r, t); return z.IsSuccess ? Results.Ok(z.Value) : StationApiEndpoints.Problem(z.Error); });
        app.MapGet("/api/scans/{id:guid}/result", async (HttpContext c, Guid id, IScanCoordinator x, IScanRunReader r, CancellationToken t) => { if (!LocalRequestPolicy.IsLocal(c.Connection.RemoteIpAddress)) return LocalOnly(); var z = await GetScanAsync(id, x, r, t); if (!z.IsSuccess) return StationApiEndpoints.Problem(z.Error); return z.Value.Status is Station.Domain.Models.ScanStatus.Pending or Station.Domain.Models.ScanStatus.Running ? Results.Accepted($"/api/scans/{id}/result", z.Value) : Results.Ok(z.Value); });
        app.MapPost("/api/scans/{id:guid}/cancel", (HttpContext c, Guid id, IScanCoordinator x) => { if (!LocalRequestPolicy.IsLocal(c.Connection.RemoteIpAddress)) return LocalOnly(); var z = x.Cancel(id); return z.IsSuccess ? Results.Accepted($"/api/scans/{id}", z.Value) : StationApiEndpoints.Problem(z.Error); });
    }

    private static IResult LocalOnly() => StationApiEndpoints.Problem(new Error("auth.local_only", "Scan administration is available only on the host."));
    private static async Task<Result<ScanOperationStatus>> GetScanAsync(Guid id, IScanCoordinator coordinator, IScanRunReader reader, CancellationToken token) { var current = coordinator.Get(id); if (current.IsSuccess) return current; var stored = await reader.FindAsync(id, token); return stored is null ? current : Result<ScanOperationStatus>.Success(new(stored.Id, stored.MediaSourceId ?? Guid.Empty, stored.Status, stored.CreatedAt, stored.CompletedAt, stored.DiscoveredFiles, stored.UpdatedFiles, stored.ErrorCount, stored.ErrorSummary)); }
    private static string? FindExecutable(string name) => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => Path.Combine(x, name)).FirstOrDefault(File.Exists);
    private static string? FindMpvExecutable() { var found = FindExecutable("mpv.exe"); if (found is not null) return found; var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages"); return Directory.Exists(root) ? Directory.EnumerateFiles(root, "mpv.exe", SearchOption.AllDirectories).FirstOrDefault() : null; }
    private sealed record StartScanRequest(Guid MediaSourceId);
}
