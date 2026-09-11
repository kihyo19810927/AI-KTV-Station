using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Station.Application.Common;
using Station.Application.Configuration;
using Station.Application.Playback;
using Station.Domain.Models;

namespace Station.Infrastructure.Playback;

public sealed class MpvPlayerAdapter : IPlayerAdapter
{
    private readonly string executablePath;
    private readonly TimeSpan commandTimeout;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> pending = new();
    private readonly Channel<PlayerEvent> events = Channel.CreateUnbounded<PlayerEvent>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = false });
    private readonly object stateGate = new();
    private Process? process;
    private NamedPipeClientStream? pipe;
    private StreamReader? reader;
    private StreamWriter? writer;
    private CancellationTokenSource? lifetime;
    private Task? readTask;
    private TaskCompletionSource<bool>? fileLoaded;
    private PlayerSnapshot snapshot = EmptySnapshot(PlayerLifecycleState.Stopped);
    private long requestId;
    private bool stopping;
    private bool suppressNextEndFile;
    private bool disposed;

    public MpvPlayerAdapter(PlayerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        executablePath = options.ExecutablePath;
        commandTimeout = TimeSpan.FromSeconds(options.CommandTimeoutSeconds);
    }

    internal int? ProcessId
    {
        get
        {
            try { return process is { HasExited: false } running ? running.Id : null; }
            catch (InvalidOperationException) { return null; }
        }
    }

    public async Task<Result<PlayerSnapshot>> StartAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsRunning()) return Result<PlayerSnapshot>.Success(CurrentSnapshot());
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return Failure("player.executable_missing", "The configured player executable is unavailable.");

            var pipeName = $"ai-ktv-station-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            startInfo.ArgumentList.Add("--idle=yes");
            startInfo.ArgumentList.Add("--no-terminal");
            // Keep the owned output window alive while mpv is idle between queued songs.
            startInfo.ArgumentList.Add("--force-window=yes");
            startInfo.ArgumentList.Add("--audio-display=no");
            startInfo.ArgumentList.Add($"--input-ipc-server=\\\\.\\pipe\\{pipeName}");

            try
            {
                lifetime = new CancellationTokenSource();
                process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.Exited += OnProcessExited;
                if (!process.Start()) return Failure("player.start_failed", "The player could not be started.");
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectionTimeout.CancelAfter(commandTimeout);
                await pipe.ConnectAsync(connectionTimeout.Token).ConfigureAwait(false);
                reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
                writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
                readTask = ReadLoopAsync(lifetime.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await CleanupProcessAsync().ConfigureAwait(false);
                return Failure("player.connection_timeout", "Timed out while connecting to the player.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await CleanupProcessAsync().ConfigureAwait(false);
                return Failure("player.start_failed", "The player could not be started.");
            }

            ChangeState(PlayerLifecycleState.Idle, null, null);
            Publish(new PlayerStartedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow));
            return Result<PlayerSnapshot>.Success(CurrentSnapshot());
        }
        finally { operationGate.Release(); }
    }

    public async Task<Result<PlayerSnapshot>> StopAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var before = CurrentSnapshot();
            if (!IsRunning())
            {
                ChangeState(PlayerLifecycleState.Stopped, null, null);
                return Result<PlayerSnapshot>.Success(CurrentSnapshot());
            }

            stopping = true;
            if (before.PlaybackId is { } playbackId && before.State is PlayerLifecycleState.Preparing or PlayerLifecycleState.Playing or PlayerLifecycleState.Paused)
                Publish(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, PlaybackEndReason.Stopped));
            try { await SendCommandAsync(cancellationToken, "quit").ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
            await CleanupProcessAsync().ConfigureAwait(false);
            ChangeState(PlayerLifecycleState.Stopped, null, null);
            stopping = false;
            return Result<PlayerSnapshot>.Success(CurrentSnapshot());
        }
        finally { operationGate.Release(); }
    }

    public async Task<Result<PlayerSnapshot>> LoadAsync(PlayerLoadRequest request, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.Validate(request);
        if (validation is not null) return Result<PlayerSnapshot>.Failure(validation);
        return await ExecuteAsync(async () =>
        {
            var before = CurrentSnapshot();
            if (before.PlaybackId is { } previous && before.State is PlayerLifecycleState.Preparing or PlayerLifecycleState.Playing or PlayerLifecycleState.Paused)
            {
                suppressNextEndFile = true;
                Publish(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, previous, PlaybackEndReason.Replaced));
            }
            ChangeState(PlayerLifecycleState.Preparing, request.PlaybackId, null);
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (stateGate) fileLoaded = loaded;
            try
            {
                await SendCommandAsync(cancellationToken, "loadfile", request.MediaPath, "replace").ConfigureAwait(false);
                await loaded.Task.WaitAsync(commandTimeout, cancellationToken).ConfigureAwait(false);
                return await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (stateGate) { if (ReferenceEquals(fileLoaded, loaded)) fileLoaded = null; }
            }
        }, "player.media_load_failed", "The media could not be loaded.", cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<PlayerSnapshot>> PlayAsync(CancellationToken cancellationToken = default) =>
        SetPauseAsync(false, cancellationToken);

    public Task<Result<PlayerSnapshot>> PauseAsync(CancellationToken cancellationToken = default) =>
        SetPauseAsync(true, cancellationToken);

    public async Task<Result<PlayerSnapshot>> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.ValidatePosition(position);
        if (validation is not null) return Result<PlayerSnapshot>.Failure(validation);
        return await ExecuteAsync(async () =>
        {
            await SendCommandAsync(cancellationToken, "seek", position.TotalSeconds, "absolute", "exact").ConfigureAwait(false);
            return await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }, "player.seek_failed", "The playback position could not be changed.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlayerSnapshot>> SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.ValidateVolume(volume);
        if (validation is not null) return Result<PlayerSnapshot>.Failure(validation);
        return await SetPropertyAndRefreshAsync("volume", volume, "player.volume_failed", "The volume could not be changed.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlayerSnapshot>> SelectAudioTrackAsync(int streamId, CancellationToken cancellationToken = default)
    {
        var validation = PlayerCommandValidation.ValidateTrack(streamId);
        if (validation is not null) return Result<PlayerSnapshot>.Failure(validation);
        return await SetPropertyAndRefreshAsync("aid", streamId, "player.audio_track_failed", "The audio track could not be selected.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlayerSnapshot>> SelectSubtitleTrackAsync(int? streamId, CancellationToken cancellationToken = default)
    {
        if (streamId is { } id && PlayerCommandValidation.ValidateTrack(id) is { } validation)
            return Result<PlayerSnapshot>.Failure(validation);
        return await SetPropertyAndRefreshAsync("sid", streamId is null ? "no" : streamId.Value,
            "player.subtitle_track_failed", "The subtitle track could not be selected.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PlayerSnapshot>> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning()) return Result<PlayerSnapshot>.Success(CurrentSnapshot());
        return await ExecuteAsync(() => RefreshSnapshotAsync(cancellationToken), "player.state_failed",
            "The player state could not be read.", cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<PlayerEvent> WatchEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return item;
    }

    private async Task<Result<PlayerSnapshot>> SetPauseAsync(bool pause, CancellationToken cancellationToken) =>
        await SetPropertyAndRefreshAsync("pause", pause, "player.pause_failed",
            "The playback state could not be changed.", cancellationToken).ConfigureAwait(false);

    private async Task<Result<PlayerSnapshot>> SetPropertyAndRefreshAsync(string property, object value, string code, string message, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await SendCommandAsync(cancellationToken, "set_property", property, value).ConfigureAwait(false);
            return await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }, code, message, cancellationToken).ConfigureAwait(false);

    private async Task<Result<PlayerSnapshot>> ExecuteAsync(
        Func<Task<PlayerSnapshot>> action, string code, string message, CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!IsRunning()) return Result<PlayerSnapshot>.Failure(new Error("player.not_started", "The player is not running."));
            try { return Result<PlayerSnapshot>.Success(await action().ConfigureAwait(false)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (TimeoutException) { return Result<PlayerSnapshot>.Failure(new Error("player.command_timeout", "The player command timed out.")); }
            catch (MpvCommandException) { return Result<PlayerSnapshot>.Failure(new Error(code, message)); }
            catch (Exception) { return Result<PlayerSnapshot>.Failure(new Error("player.protocol_error", "The player communication failed.")); }
        }
        finally { operationGate.Release(); }
    }

    private async Task<PlayerSnapshot> RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        var pause = await GetPropertyAsync("pause", cancellationToken).ConfigureAwait(false);
        var position = await GetOptionalDoublePropertyAsync("time-pos", cancellationToken).ConfigureAwait(false);
        var duration = await GetOptionalDoublePropertyAsync("duration", cancellationToken).ConfigureAwait(false);
        var volume = await GetOptionalDoublePropertyAsync("volume", cancellationToken).ConfigureAwait(false) ?? CurrentSnapshot().Volume;
        var audioId = ParseOptionalId(await GetPropertyAsync("aid", cancellationToken).ConfigureAwait(false));
        var subtitleId = ParseOptionalId(await GetPropertyAsync("sid", cancellationToken).ConfigureAwait(false));
        var tracksElement = await GetPropertyAsync("track-list", cancellationToken).ConfigureAwait(false);
        var tracks = ParseTracks(tracksElement);
        var current = CurrentSnapshot();
        var activeId = current.PlaybackId;
        var nextState = current.State is PlayerLifecycleState.Ended or PlayerLifecycleState.Failed
            ? current.State
            : activeId is null
                ? PlayerLifecycleState.Idle
                : pause.ValueKind == JsonValueKind.True ? PlayerLifecycleState.Paused : PlayerLifecycleState.Playing;
        var next = new PlayerSnapshot(nextState, activeId, TimeSpan.FromSeconds(position ?? 0),
            duration is null ? null : TimeSpan.FromSeconds(duration.Value), volume, audioId, subtitleId, tracks, current.Failure);
        SetSnapshot(next);
        return next;
    }

    private async Task<JsonElement> GetPropertyAsync(string name, CancellationToken cancellationToken)
    {
        var response = await SendCommandAsync(cancellationToken, "get_property", name).ConfigureAwait(false);
        return response.TryGetProperty("data", out var data) ? data.Clone() : default;
    }

    private async Task<double?> GetOptionalDoublePropertyAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var value = await GetPropertyAsync(name, cancellationToken).ConfigureAwait(false);
            return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
        }
        catch (MpvCommandException) { return null; }
    }

    private async Task<JsonElement> SendCommandAsync(CancellationToken cancellationToken, params object[] command)
    {
        var output = writer ?? throw new MpvCommandException("not connected");
        var id = Interlocked.Increment(ref requestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, completion)) throw new MpvCommandException("duplicate request id");
        try
        {
            var line = JsonSerializer.Serialize(new { command, request_id = id });
            await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await output.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false); }
            finally { writeGate.Release(); }
            var response = await completion.Task.WaitAsync(commandTimeout, cancellationToken).ConfigureAwait(false);
            if (!response.TryGetProperty("error", out var error) || !string.Equals(error.GetString(), "success", StringComparison.Ordinal))
                throw new MpvCommandException(error.GetString() ?? "invalid response");
            return response;
        }
        catch (TimeoutException) { throw; }
        finally { pending.TryRemove(id, out _); }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && await reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("request_id", out var id) && id.TryGetInt64(out var value) && pending.TryRemove(value, out var completion))
                    completion.TrySetResult(root.Clone());
                if (root.TryGetProperty("event", out var eventName)) HandleMpvEvent(eventName.GetString(), root);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; }
        finally
        {
            var exception = failure ?? new EndOfStreamException("mpv IPC closed");
            foreach (var completion in pending.Values) completion.TrySetException(exception);
            if (!stopping && !disposed) HandleUnexpectedExit();
        }
    }

    private void HandleMpvEvent(string? eventName, JsonElement root)
    {
        if (eventName == "file-loaded")
        {
            TaskCompletionSource<bool>? completion;
            lock (stateGate) completion = fileLoaded;
            completion?.TrySetResult(true);
            return;
        }
        if (eventName != "end-file") return;
        if (suppressNextEndFile) { suppressNextEndFile = false; return; }
        var current = CurrentSnapshot();
        if (current.PlaybackId is not { } playbackId) return;
        var reason = root.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() : null;
        if (reason == "error")
        {
            var failure = new PlayerFailure("player.media_load_failed", PlayerFailureKind.MediaLoadFailed, true, "The media could not be played.");
            lock (stateGate) fileLoaded?.TrySetException(new MpvCommandException("media load failed"));
            ChangeState(PlayerLifecycleState.Failed, playbackId, failure);
            Publish(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, failure));
            Publish(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, PlaybackEndReason.Failed));
            return;
        }
        ChangeState(PlayerLifecycleState.Ended, playbackId, null);
        Publish(new PlaybackEndedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId,
            reason == "eof" ? PlaybackEndReason.Completed : PlaybackEndReason.Stopped));
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        foreach (var completion in pending.Values) completion.TrySetException(new MpvCommandException("process exited"));
        if (!stopping && !disposed) HandleUnexpectedExit();
    }

    private void HandleUnexpectedExit()
    {
        var current = CurrentSnapshot();
        if (current.State is PlayerLifecycleState.Failed or PlayerLifecycleState.Stopped) return;
        var failure = new PlayerFailure("player.process_exited", PlayerFailureKind.ProcessExited, true, "The player stopped unexpectedly.");
        ChangeState(PlayerLifecycleState.Failed, current.PlaybackId, failure);
        Publish(new PlaybackFailedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, current.PlaybackId, failure));
    }

    private void ChangeState(PlayerLifecycleState state, Guid? playbackId, PlayerFailure? failure)
    {
        PlayerSnapshot previous;
        PlayerSnapshot next;
        lock (stateGate)
        {
            previous = snapshot;
            next = state is PlayerLifecycleState.Stopped or PlayerLifecycleState.Idle
                ? EmptySnapshot(state)
                : snapshot with { State = state, PlaybackId = playbackId, Failure = failure };
            snapshot = next;
        }
        if (previous.State != next.State || previous.PlaybackId != next.PlaybackId)
            Publish(new PlayerStateChangedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, next.PlaybackId, previous.State, next.State));
    }

    private void SetSnapshot(PlayerSnapshot next)
    {
        PlayerSnapshot previous;
        lock (stateGate) { previous = snapshot; snapshot = next; }
        if (previous.State != next.State)
            Publish(new PlayerStateChangedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, next.PlaybackId, previous.State, next.State));
        if (next.PlaybackId is { } playbackId && !TracksEqual(previous.Tracks, next.Tracks))
            Publish(new PlayerTracksChangedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, playbackId, next.Tracks));
        if (next.PlaybackId is { } startedId && previous.State is PlayerLifecycleState.Preparing && next.State is PlayerLifecycleState.Playing)
            Publish(new PlaybackStartedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, startedId));
    }

    private async Task CleanupProcessAsync()
    {
        lifetime?.Cancel();
        try { writer?.Dispose(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        try { reader?.Dispose(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        pipe?.Dispose();
        if (process is { HasExited: false } running)
        {
            try { await running.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                try { running.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                try { await running.WaitForExitAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
            }
        }
        if (readTask is not null)
        {
            try { await readTask.ConfigureAwait(false); } catch (Exception) { }
        }
        process?.Dispose();
        process = null;
        reader = null;
        writer = null;
        pipe = null;
        lifetime?.Dispose();
        lifetime = null;
        readTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        stopping = true;
        await CleanupProcessAsync().ConfigureAwait(false);
        disposed = true;
        events.Writer.TryComplete();
        operationGate.Dispose();
        writeGate.Dispose();
    }

    private bool IsRunning() => process is { HasExited: false } && pipe is { IsConnected: true };
    private PlayerSnapshot CurrentSnapshot() { lock (stateGate) return snapshot; }
    private void Publish(PlayerEvent item) => events.Writer.TryWrite(item);
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    private static Result<PlayerSnapshot> Failure(string code, string message) => Result<PlayerSnapshot>.Failure(new Error(code, message));
    private static PlayerSnapshot EmptySnapshot(PlayerLifecycleState state) => new(state, null, TimeSpan.Zero, null, 100, null, null, []);
    private static int? ParseOptionalId(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) ? id : null;
    private static bool TracksEqual(IReadOnlyList<PlayerTrack> left, IReadOnlyList<PlayerTrack> right) => left.SequenceEqual(right);

    private static IReadOnlyList<PlayerTrack> ParseTracks(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<PlayerTrack>();
        foreach (var item in value.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id)) continue;
            var type = GetString(item, "type") switch
            {
                "video" => MediaTrackType.Video,
                "audio" => MediaTrackType.Audio,
                "sub" => MediaTrackType.Subtitle,
                _ => (MediaTrackType?)null,
            };
            if (type is null) continue;
            var selected = item.TryGetProperty("selected", out var selectedElement) && selectedElement.ValueKind == JsonValueKind.True;
            result.Add(new PlayerTrack(id, type.Value, GetString(item, "codec"), GetString(item, "lang"), GetString(item, "title"), selected));
        }
        return result;
    }

    private static string? GetString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed class MpvCommandException(string message) : Exception(message);
}
