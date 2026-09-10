using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Health;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using System.Net;
using System.Net.Sockets;

namespace Station.Infrastructure.Health;

public sealed class StationHealthService(StationDbContext database, StationOptions options, TimeProvider timeProvider) : IStationHealthService
{
    public async Task<StationHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<HealthComponent>
        {
            await CheckServiceAsync(cancellationToken),
            await CheckDatabaseAsync(cancellationToken),
            await CheckSourcesAsync(cancellationToken),
            CheckPlayer(),
        };
        return new(timeProvider.GetUtcNow(), components);
    }

    private async Task<HealthComponent> CheckServiceAsync(CancellationToken token)
    {
        try
        {
            var configured = IPAddress.Parse(options.Server.BindAddress);
            var target = configured.Equals(IPAddress.Any) ? IPAddress.Loopback : configured.Equals(IPAddress.IPv6Any) ? IPAddress.IPv6Loopback : configured;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            using var client = new TcpClient(target.AddressFamily);
            await client.ConnectAsync(target, options.Server.Port, timeout.Token);
            return new("点歌服务", HealthLevel.Healthy, "内嵌服务可连接");
        }
        catch { return new("点歌服务", HealthLevel.Unavailable, "内嵌服务暂不可连接"); }
    }

    private async Task<HealthComponent> CheckDatabaseAsync(CancellationToken token)
    {
        try { return await database.Database.CanConnectAsync(token) ? new("本地数据库", HealthLevel.Healthy, "SQLite 可连接") : new("本地数据库", HealthLevel.Unavailable, "SQLite 暂不可连接"); }
        catch { return new("本地数据库", HealthLevel.Unavailable, "SQLite 检查失败"); }
    }

    private async Task<HealthComponent> CheckSourcesAsync(CancellationToken token)
    {
        try
        {
            var states = await database.MediaSources.AsNoTracking().Where(x => x.IsEnabled).Select(x => x.Availability).ToListAsync(token);
            if (states.Count == 0) return new("媒体挂载", HealthLevel.Warning, "尚未配置媒体源");
            var available = states.Count(x => x == AvailabilityStatus.Available);
            return available == states.Count ? new("媒体挂载", HealthLevel.Healthy, $"{available} 个媒体源可用") : new("媒体挂载", HealthLevel.Warning, $"{available}/{states.Count} 个媒体源可用");
        }
        catch { return new("媒体挂载", HealthLevel.Unavailable, "媒体源状态暂不可读取"); }
    }

    private HealthComponent CheckPlayer()
    {
        var path = options.Player.ExecutablePath;
        var found = !string.IsNullOrWhiteSpace(path) ? File.Exists(path) : FindExecutable("mpv.exe") is not null || FindWingetMpv() is not null;
        return found ? new("mpv 播放器", HealthLevel.Healthy, "播放器可执行文件已就绪") : new("mpv 播放器", HealthLevel.Unavailable, "未找到 mpv.exe");
    }

    private static string? FindExecutable(string name) => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => Path.Combine(x, name)).FirstOrDefault(File.Exists);
    private static string? FindWingetMpv() { var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages"); return Directory.Exists(root) ? Directory.EnumerateFiles(root, "mpv.exe", SearchOption.AllDirectories).FirstOrDefault() : null; }
}
