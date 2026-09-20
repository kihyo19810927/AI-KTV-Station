using Station.Server.Hosting;

namespace Station.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly TrayStationController controller;
    private bool shuttingDown;
    private bool startupCompleted;
    private TimeSpan? smokeExitDelay;
    private System.Threading.Timer? smokeExitTimer;

    public TrayApplicationContext(TrayStationController controller)
    {
        this.controller = controller;
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开桌面点歌", null, async (_, _) => await OpenDesktopAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出 AI-KTV Station", null, (_, _) => ExitThread());
        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AI-KTV Station",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _ = StartAsync();
    }

    protected override void ExitThreadCore()
    {
        if (shuttingDown)
        {
            base.ExitThreadCore();
            return;
        }

        BeginShutdown();
    }

    public void ScheduleSmokeExit(TimeSpan delay)
    {
        smokeExitDelay = delay;
        if (Volatile.Read(ref startupCompleted)) ExitAfter(delay);
    }

    private void ExitAfter(TimeSpan delay)
    {
        // Startup completion may resume on a worker thread. A Forms.Timer then
        // has no message pump, so use a thread-pool timer for smoke-only exit.
        smokeExitTimer = new System.Threading.Timer(static state =>
        {
            var context = (TrayApplicationContext)state!;
            context.BeginShutdown();
        }, this, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task StartAsync()
    {
        try
        {
            var opened = await controller.StartAsync();
            if (!opened) notifyIcon.ShowBalloonTip(5000, "AI-KTV Station", "服务已启动；未能自动打开 Edge，请从托盘菜单打开桌面点歌。", ToolTipIcon.Info);
            Volatile.Write(ref startupCompleted, true);
            if (smokeExitDelay is { } delay) ExitAfter(delay);
        }
        catch (Exception exception)
        {
            notifyIcon.ShowBalloonTip(8000, "AI-KTV Station 启动失败", $"{exception.GetType().Name}。请查看本机诊断。", ToolTipIcon.Error);
        }
    }

    private async Task StopThenExitAsync()
    {
        try
        {
            var shutdown = controller.DisposeAsync().AsTask();
            if (await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(10))) == shutdown)
                await shutdown;
        }
        finally
        {
            notifyIcon.Dispose();
            // The notification-area message pump can be in an exit transition
            // already. Do not leave this process alive if that pump ignores a
            // cross-thread ExitThread request after the controlled shutdown.
            Environment.Exit(0);
        }
    }

    private void BeginShutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        smokeExitTimer?.Dispose();
        smokeExitTimer = null;
        notifyIcon.Visible = false;
        _ = StopThenExitAsync();
    }

    private async Task OpenDesktopAsync()
    {
        if (!await controller.OpenDesktopAsync())
            notifyIcon.ShowBalloonTip(5000, "AI-KTV Station", "服务尚未启动或 Edge 不可用。", ToolTipIcon.Warning);
    }
}
