using Station.Server.Hosting;

namespace Station.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly TrayStationController controller;

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
        notifyIcon.Visible = false;
        controller.DisposeAsync().AsTask().GetAwaiter().GetResult();
        notifyIcon.Dispose();
        base.ExitThreadCore();
    }

    private async Task StartAsync()
    {
        try
        {
            var opened = await controller.StartAsync();
            if (!opened) notifyIcon.ShowBalloonTip(5000, "AI-KTV Station", "服务已启动；未能自动打开 Edge，请从托盘菜单打开桌面点歌。", ToolTipIcon.Info);
        }
        catch (Exception exception)
        {
            notifyIcon.ShowBalloonTip(8000, "AI-KTV Station 启动失败", $"{exception.GetType().Name}。请查看本机诊断。", ToolTipIcon.Error);
        }
    }

    private async Task OpenDesktopAsync()
    {
        if (!await controller.OpenDesktopAsync())
            notifyIcon.ShowBalloonTip(5000, "AI-KTV Station", "服务尚未启动或 Edge 不可用。", ToolTipIcon.Warning);
    }
}
