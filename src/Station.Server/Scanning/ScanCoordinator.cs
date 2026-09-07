using Microsoft.Extensions.DependencyInjection;
using Station.Application.Common;
using Station.Application.Scanning;
using Station.Application.Search;
using Station.Domain.Models;

namespace Station.Server.Scanning;

public sealed class ScanCoordinator(IServiceScopeFactory scopeFactory) : IScanCoordinator
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Operation> operations = [];

    public Result<ScanOperationStatus> Start(Guid mediaSourceId)
    {
        if (mediaSourceId == Guid.Empty) return Failure("scan.invalid_source_id", "Media source id is required.");
        Operation operation;
        lock (gate)
        {
            if (operations.Values.Any(x => x.Status.MediaSourceId == mediaSourceId && IsActive(x.Status.Status)))
                return Failure("scan.already_running", "A scan for this media source is already running.");
            var now = DateTimeOffset.UtcNow;
            var status = new ScanOperationStatus(Guid.NewGuid(), mediaSourceId, ScanStatus.Pending, now, null, 0, 0, 0, null);
            operation = new Operation(status, new CancellationTokenSource());
            operations.Add(status.ScanRunId, operation);
        }
        _ = Task.Run(() => ExecuteAsync(operation), CancellationToken.None);
        return Result<ScanOperationStatus>.Success(operation.Status);
    }

    public Result<ScanOperationStatus> Get(Guid scanRunId)
    {
        lock (gate)
            return operations.TryGetValue(scanRunId, out var operation)
                ? Result<ScanOperationStatus>.Success(operation.Status)
                : Failure("scan.operation_not_found", "Scan operation was not found.");
    }

    public Result<ScanOperationStatus> Cancel(Guid scanRunId)
    {
        lock (gate)
        {
            if (!operations.TryGetValue(scanRunId, out var operation))
                return Failure("scan.operation_not_found", "Scan operation was not found.");
            if (!IsActive(operation.Status.Status))
                return Failure("scan.operation_finished", "Scan operation has already finished.");
            operation.Cancellation.Cancel();
            return Result<ScanOperationStatus>.Success(operation.Status);
        }
    }

    private async Task ExecuteAsync(Operation operation)
    {
        Update(operation, operation.Status with { Status = ScanStatus.Running });
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<IMediaScanRunner>();
            var progress = new InlineProgress<MediaScanProgress>(value => Update(operation, operation.Status with
            {
                Status = value.Status,
                DiscoveredFiles = value.DiscoveredFiles,
                UpdatedFiles = value.UpdatedFiles,
                ErrorCount = value.ErrorCount,
            }));
            var result = await runner.ScanAsync(operation.Status.MediaSourceId, operation.Status.ScanRunId, progress, operation.Cancellation.Token);
            if (!result.IsSuccess)
            {
                Finish(operation, ScanStatus.Failed, result.Error.Code);
                return;
            }
            if (result.Value.Status == ScanStatus.Completed)
                await scope.ServiceProvider.GetRequiredService<ISongSearchIndex>().RebuildAsync(operation.Cancellation.Token);
            Finish(operation, result.Value.Status, result.Value.ErrorSummary);
        }
        catch (OperationCanceledException)
        {
            Finish(operation, ScanStatus.Cancelled, null);
        }
        catch (Exception)
        {
            Finish(operation, ScanStatus.Failed, "scan.unhandled_error");
        }
        finally
        {
            operation.Cancellation.Dispose();
        }
    }

    private void Finish(Operation operation, ScanStatus status, string? errorCode) => Update(operation, operation.Status with
    {
        Status = status,
        CompletedAt = DateTimeOffset.UtcNow,
        ErrorCode = errorCode,
    });

    private void Update(Operation operation, ScanOperationStatus status)
    {
        lock (gate) operation.Status = status;
    }

    private static bool IsActive(ScanStatus status) => status is ScanStatus.Pending or ScanStatus.Running;
    private static Result<ScanOperationStatus> Failure(string code, string message) => Result<ScanOperationStatus>.Failure(new(code, message));

    private sealed class Operation(ScanOperationStatus status, CancellationTokenSource cancellation)
    {
        public ScanOperationStatus Status { get; set; } = status;
        public CancellationTokenSource Cancellation { get; } = cancellation;
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
