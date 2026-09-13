using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Station.Application.Playback;

namespace Station.Server.Hosting;

public interface ITrayStationRuntime : IAsyncDisposable
{
    IServiceProvider Services { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IEdgeAppLauncher
{
    bool TryOpen(Uri address);
}

public sealed class EdgeAppLauncher : IEdgeAppLauncher
{
    public bool TryOpen(Uri address)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "msedge.exe",
                Arguments = $"--app=\"{address.AbsoluteUri}\" --start-maximized",
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}

public sealed class WebApplicationTrayRuntime(WebApplication application) : ITrayStationRuntime
{
    public IServiceProvider Services => application.Services;
    public Task InitializeAsync(CancellationToken cancellationToken = default) => StationServerHost.InitializeAsync(application.Services, cancellationToken);
    public Task StartAsync(CancellationToken cancellationToken = default) => application.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => application.StopAsync(cancellationToken);
    public ValueTask DisposeAsync() => application.DisposeAsync();
}

/// <summary>
/// Owns only the Station host and its registered player. It never kills arbitrary
/// mpv processes by image name; the adapter stops the child process it created.
/// </summary>
public sealed class TrayStationController(ITrayStationRuntime runtime, IPlayerAdapter player, IEdgeAppLauncher edge, Uri desktopUri) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool started;
    private bool disposed;

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (started) return true;
            await runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
            started = true;
            return edge.TryOpen(desktopUri);
        }
        finally { gate.Release(); }
    }

    public Task<bool> OpenDesktopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Task.FromResult(started && edge.TryOpen(desktopUri));
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!started) return;
            try { await player.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch { /* Host shutdown continues so the adapter can dispose its owned process. */ }
            await runtime.StopAsync(cancellationToken).ConfigureAwait(false);
            started = false;
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        try { await StopAsync(); }
        finally
        {
            disposed = true;
            await runtime.DisposeAsync().ConfigureAwait(false);
            gate.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(TrayStationController));
    }
}
