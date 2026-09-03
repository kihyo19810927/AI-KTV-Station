using System.Diagnostics;
using System.Text;
using System.Text.Json;

if (args.Length < 2) { Console.Error.WriteLine("Usage: Station.PlayerSpike <mpv.exe> <media>"); return 2; }
var mpv = new MpvProcess(args[0]);
try
{
    await mpv.StartAsync();
    await mpv.CommandAsync("loadfile", args[1], "replace");
    await mpv.WaitForEventAsync("file-loaded", TimeSpan.FromSeconds(10));
    var tracks = await mpv.GetPropertyAsync("track-list");
    Console.WriteLine($"TRACKS={tracks.GetArrayLength()}");
    int? vocal = null, backing = null, subtitle = null;
    foreach (var track in tracks.EnumerateArray()) {
        var type = track.GetProperty("type").GetString(); var trackId = track.GetProperty("id").GetInt32();
        var title = track.TryGetProperty("title", out var titleValue) ? titleValue.GetString() ?? "" : "";
        Console.WriteLine($"TRACK {type} {trackId} {title}");
        if (type == "audio" && title.Contains("原唱")) vocal = trackId;
        if (type == "audio" && title.Contains("伴奏")) backing = trackId;
        if (type == "sub") subtitle = trackId;
    }
    if (vocal is null) vocal = 1; if (backing is null) backing = 2; if (subtitle is null) subtitle = 1;
    await mpv.SetPropertyAsync("aid", vocal.Value); await mpv.SetPropertyAsync("aid", backing.Value); await mpv.SetPropertyAsync("sid", subtitle.Value);
    var time = await mpv.GetPropertyAsync("time-pos"); Console.WriteLine($"TIME_POS={time}");
    await mpv.WaitForEventAsync("end-file", TimeSpan.FromSeconds(15));
    Console.WriteLine("END_FILE=received");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine($"SPIKE_ERROR={ex.Message}"); return 1; }
finally { await mpv.DisposeAsync(); }

sealed class MpvProcess : IAsyncDisposable
{
    readonly string executable; Process? process; Stream? channel;
    readonly SemaphoreSlim gate = new(1, 1); int id;
    public MpvProcess(string executable) => this.executable = executable;
    public async Task StartAsync()
    {
        var pipe = $"ai-ktv-spike-{Environment.ProcessId}";
        process = Process.Start(new ProcessStartInfo(executable, $"--idle=yes --no-video --really-quiet --input-ipc-server=\\\\.\\pipe\\{pipe}") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }) ?? throw new InvalidOperationException("mpv did not start");
        var client = new System.IO.Pipes.NamedPipeClientStream(".", pipe, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(5000); channel = client; _ = ReadAsync(new StreamReader(client, Encoding.UTF8));
        await CommandAsync("observe_property", 1, "time-pos");
    }
    readonly Dictionary<int, TaskCompletionSource<JsonElement>> pending = [];
    async Task ReadAsync(StreamReader output)
    {
        while (await output.ReadLineAsync() is { } line) { using var doc = JsonDocument.Parse(line); var root = doc.RootElement; if (root.TryGetProperty("event", out var ev)) { if (ev.GetString()=="end-file") endFile.TrySetResult(true); if (ev.GetString()=="file-loaded") fileLoaded.TrySetResult(true); } if (root.TryGetProperty("request_id", out var rid) && pending.Remove(rid.GetInt32(), out var tcs)) tcs.TrySetResult(root.Clone()); }
        exited.TrySetResult(true);
    }
    readonly TaskCompletionSource<bool> fileLoaded = new(), endFile = new(), exited = new();
    public async Task<JsonElement> GetPropertyAsync(string name) { var r = await RequestAsync(new { command = new object[] { "get_property", name } }); return r.GetProperty("data").Clone(); }
    public Task SetPropertyAsync(string name, object value) => CommandAsync("set_property", name, value);
    public Task CommandAsync(params object[] command) => RequestAsync(new { command });
    async Task<JsonElement> RequestAsync(object message)
    {
        await gate.WaitAsync(); try { var requestId = Interlocked.Increment(ref id); var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously); pending[requestId] = tcs; var command = (object[])message.GetType().GetProperty("command")!.GetValue(message)!; var request = JsonSerializer.SerializeToUtf8Bytes(new { command, request_id = requestId }); await channel!.WriteAsync(request); await channel.WriteAsync("\n"u8.ToArray()); await channel.FlushAsync(); return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { gate.Release(); }
    }
    public async Task WaitForEventAsync(string name, TimeSpan timeout) => await (name == "end-file" ? endFile.Task : fileLoaded.Task).WaitAsync(timeout);
    public async ValueTask DisposeAsync() { if (process is not null && !process.HasExited) { try { process.Kill(); } catch { } await process.WaitForExitAsync(); } process?.Dispose(); }
}
