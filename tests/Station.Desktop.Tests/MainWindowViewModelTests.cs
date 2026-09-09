using Station.Desktop.ViewModels;
using Station.Application.Health;

namespace Station.Desktop.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Defines_all_v1_navigation_destinations()
    {
        var viewModel = new MainWindowViewModel();
        Assert.Equal(Enum.GetValues<DesktopPage>(), viewModel.NavigationItems.Select(item => item.Page));
        Assert.Equal(DesktopPage.Dashboard, viewModel.CurrentPage);
        Assert.Equal("仪表盘", viewModel.CurrentTitle);
    }

    [Fact]
    public void Navigation_command_updates_current_page_and_display_properties()
    {
        var viewModel = new MainWindowViewModel();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.NavigateCommand.Execute(DesktopPage.Room);

        Assert.Equal(DesktopPage.Room, viewModel.CurrentPage);
        Assert.Equal("房间与二维码", viewModel.CurrentTitle);
        Assert.Contains(nameof(MainWindowViewModel.CurrentPage), changes);
        Assert.Contains(nameof(MainWindowViewModel.CurrentTitle), changes);
        Assert.Contains(nameof(MainWindowViewModel.CurrentDescription), changes);
    }

    [Fact]
    public async Task Refresh_health_exposes_safe_component_snapshot()
    {
        var service = new StubHealthService(new StationHealthSnapshot(DateTimeOffset.UtcNow,
            [new HealthComponent("本地数据库", HealthLevel.Healthy, "SQLite 可连接"), new HealthComponent("媒体挂载", HealthLevel.Warning, "尚未配置媒体源")]));
        var viewModel = new MainWindowViewModel(service);

        await viewModel.RefreshHealthAsync();

        Assert.Equal(2, viewModel.HealthComponents.Count);
        Assert.Equal("部分功能待就绪", viewModel.HealthStatus);
        Assert.DoesNotContain(viewModel.HealthComponents, x => x.Summary.Contains(":\\", StringComparison.Ordinal));
    }

    private sealed class StubHealthService(StationHealthSnapshot snapshot) : IStationHealthService
    {
        public Task<StationHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }
}
