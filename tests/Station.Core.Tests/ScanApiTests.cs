using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Scanning;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Core.Tests;

public sealed class ScanApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Create_progress_cancel_and_result_contracts_do_not_expose_paths()
    {
        await using var factory = new ScanApiFactory();
        using var client = factory.CreateClient();
        var sourceId = Guid.NewGuid();

        var create = await client.PostAsJsonAsync("/api/scans", new { mediaSourceId = sourceId });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var started = await create.Content.ReadFromJsonAsync<ScanOperationStatus>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal($"/api/scans/{started.ScanRunId}", create.Headers.Location?.OriginalString);

        var progress = await client.GetFromJsonAsync<ScanOperationStatus>($"/api/scans/{started.ScanRunId}", JsonOptions);
        Assert.Equal(7, progress!.DiscoveredFiles);
        var pendingResult = await client.GetAsync($"/api/scans/{started.ScanRunId}/result");
        Assert.Equal(HttpStatusCode.Accepted, pendingResult.StatusCode);

        var cancel = await client.PostAsync($"/api/scans/{started.ScanRunId}/cancel", null);
        Assert.Equal(HttpStatusCode.Accepted, cancel.StatusCode);
        var final = await client.GetFromJsonAsync<ScanOperationStatus>($"/api/scans/{started.ScanRunId}/result", JsonOptions);
        Assert.Equal(ScanStatus.Cancelled, final!.Status);
        Assert.DoesNotContain(typeof(ScanOperationStatus).GetProperties(), property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/scans/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Real_coordinator_scans_empty_temporary_source_and_returns_persisted_run_id()
    {
        await using var factory = new RealScanApiFactory();
        var mediaRoot = Path.Combine(factory.DataDirectory, "空曲库");
        Directory.CreateDirectory(mediaRoot);
        Guid sourceId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            var source = new MediaSource { Name = "Empty fixture", RootPath = mediaRoot, Availability = AvailabilityStatus.Available };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync();
            sourceId = source.Id;
        }
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/scans", new { mediaSourceId = sourceId });
        var started = await response.Content.ReadFromJsonAsync<ScanOperationStatus>(JsonOptions);
        Assert.NotNull(started);

        ScanOperationStatus current;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        do
        {
            await Task.Delay(20, timeout.Token);
            current = (await client.GetFromJsonAsync<ScanOperationStatus>($"/api/scans/{started.ScanRunId}/result", JsonOptions, timeout.Token))!;
        } while (current.Status is ScanStatus.Pending or ScanStatus.Running);

        Assert.Equal(ScanStatus.Completed, current.Status);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var stored = await verificationScope.ServiceProvider.GetRequiredService<StationDbContext>().ScanRuns.SingleAsync(x => x.Id == started.ScanRunId);
        Assert.Equal(ScanStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task Result_endpoint_reads_persisted_history_when_operation_is_not_in_memory()
    {
        await using var factory = new ScanApiFactory();
        var run = new ScanRun
        {
            MediaSourceId = Guid.NewGuid(),
            Status = ScanStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            DiscoveredFiles = 12,
            UpdatedFiles = 4,
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            database.ScanRuns.Add(run);
            await database.SaveChangesAsync();
        }
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<ScanOperationStatus>($"/api/scans/{run.Id}/result", JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(ScanStatus.Completed, result.Status);
        Assert.Equal(12, result.DiscoveredFiles);
    }

    private sealed class ScanApiFactory : WebApplicationFactory<Program>
    {
        private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-ktv-api-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Station:Storage:DataDirectory", dataDirectory);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IScanCoordinator>();
                services.AddSingleton<IScanCoordinator, FakeCoordinator>();
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, true);
        }
    }

    private sealed class RealScanApiFactory : WebApplicationFactory<Program>
    {
        public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), $"ai-ktv-real-api-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("Station:Storage:DataDirectory", DataDirectory);

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, true);
        }
    }

    private sealed class FakeCoordinator : IScanCoordinator
    {
        private ScanOperationStatus? status;

        public Result<ScanOperationStatus> Start(Guid mediaSourceId)
        {
            status = new ScanOperationStatus(Guid.NewGuid(), mediaSourceId, ScanStatus.Running, DateTimeOffset.UtcNow, null, 7, 2, 0, null);
            return Result<ScanOperationStatus>.Success(status);
        }

        public Result<ScanOperationStatus> Get(Guid scanRunId) => status?.ScanRunId == scanRunId
            ? Result<ScanOperationStatus>.Success(status)
            : Result<ScanOperationStatus>.Failure(new Error("scan.operation_not_found", "Not found."));

        public Result<ScanOperationStatus> Cancel(Guid scanRunId)
        {
            var current = Get(scanRunId);
            if (!current.IsSuccess) return current;
            status = current.Value with { Status = ScanStatus.Cancelled, CompletedAt = DateTimeOffset.UtcNow };
            return Result<ScanOperationStatus>.Success(status);
        }
    }
}
