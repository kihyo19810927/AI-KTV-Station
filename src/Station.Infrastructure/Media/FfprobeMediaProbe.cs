using System.Diagnostics;
using System.Text;
using Station.Application.Common;
using Station.Application.Media;

namespace Station.Infrastructure.Media;

public sealed class FfprobeMediaProbe(string executablePath, TimeSpan timeout) : IMediaProbe
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
        using var process = Process.Start(startInfo);
        if (process is null) return Failure("media_probe.start_failed");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        try { await process.WaitForExitAsync(linked.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return Failure("media_probe.timeout");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        var output = await outputTask;
        _ = await errorTask;
        return process.ExitCode == 0 ? FfprobeJsonParser.Parse(output) : Failure("media_probe.process_failed");
    }

    private static void TryKill(Process process) { if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } } }
    private static Result<MediaProbeResult> Failure(string code) => Result<MediaProbeResult>.Failure(new Error(code, "Media metadata could not be read."));
}
