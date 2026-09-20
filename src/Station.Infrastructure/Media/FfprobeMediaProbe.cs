using System.Diagnostics;
using System.Text;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Media;

namespace Station.Infrastructure.Media;

public sealed class FfprobeMediaProbe(string executablePath, TimeSpan timeout, ILocalDiagnosticLog? diagnosticLog = null) : IMediaProbe
{
    public async Task<Result<MediaProbeResult>> ProbeAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath)) return Failure("media_probe.executable_missing");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-v", "error", "-show_entries", "format=duration:stream=index,codec_type,codec_name:stream_tags=title,language", "-of", "json", mediaPath }) startInfo.ArgumentList.Add(argument);
        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo);
        if (process is null) return Failure("media_probe.start_failed", "ffprobe process did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        try { await process.WaitForExitAsync(linked.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            await TraceAsync("media_probe.timeout", $"ffprobe timed out after {stopwatch.Elapsed.TotalSeconds:F1}s; file={SafeFileName(mediaPath)}").ConfigureAwait(false);
            return Failure("media_probe.timeout", $"ffprobe timed out after {timeout.TotalSeconds:F0} seconds.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        var output = await outputTask;
        var stderr = Compact(await errorTask);
        if (process.ExitCode == 0) return FfprobeJsonParser.Parse(output);
        var detail = string.IsNullOrWhiteSpace(stderr)
            ? $"ffprobe exited with code {process.ExitCode}."
            : $"ffprobe exited with code {process.ExitCode}: {stderr}";
        await TraceAsync("media_probe.process_failed", $"{detail} file={SafeFileName(mediaPath)}; elapsed={stopwatch.Elapsed.TotalSeconds:F1}s").ConfigureAwait(false);
        return Failure("media_probe.process_failed", detail);
    }

    private static void TryKill(Process process) { if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } } }
    private static string SafeFileName(string path) => Path.GetFileName(path).Replace('\r', ' ').Replace('\n', ' ');
    private static string Compact(string value)
    {
        var compact = string.Join(" ", (value ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return compact.Length <= 240 ? compact : compact[..240];
    }
    private async Task TraceAsync(string code, string message)
    {
        if (diagnosticLog is null) return;
        try { await diagnosticLog.WriteAsync("Warning", code, message).ConfigureAwait(false); }
        catch (Exception) { }
    }
    private static Result<MediaProbeResult> Failure(string code, string? detail = null) =>
        Result<MediaProbeResult>.Failure(new Error(code, detail ?? "Media metadata could not be read."));
}
