using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Health;

namespace Station.Desktop.ViewModels;

public enum DesktopPage { Dashboard, NowPlaying, Queue, Catalog, Room, Settings }
public sealed record NavigationItem(DesktopPage Page, string Title, string Description);

public sealed class MainWindowViewModel : ObservableObject
{
    private NavigationItem current;
    private readonly IStationHealthService? healthService;
    private string healthStatus = "正在检查本机状态…";
    private IReadOnlyList<HealthComponent> healthComponents = [];
    public MainWindowViewModel(IStationHealthService? healthService = null)
    {
        this.healthService = healthService;
        NavigationItems = new ReadOnlyCollection<NavigationItem>([new(DesktopPage.Dashboard, "仪表盘", "服务、播放器和曲库运行状态"), new(DesktopPage.NowPlaying, "正在播放", "播放、音轨、字幕、音量和进度"), new(DesktopPage.Queue, "点歌队列", "调整顺序、置顶、删除和插播"), new(DesktopPage.Catalog, "曲库管理", "搜索、扫描和元数据修正"), new(DesktopPage.Room, "房间与二维码", "开关房间、访客和点歌规则"), new(DesktopPage.Settings, "设置与诊断", "路径、端口、日志和恢复建议")]);
        current = NavigationItems[0];
        NavigateCommand = new RelayCommand<DesktopPage>(Navigate);
        RefreshHealthCommand = new AsyncRelayCommand(RefreshHealthAsync);
    }
    public ReadOnlyCollection<NavigationItem> NavigationItems { get; }
    public ICommand NavigateCommand { get; }
    public ICommand RefreshHealthCommand { get; }
    public IReadOnlyList<HealthComponent> HealthComponents { get => healthComponents; private set => SetProperty(ref healthComponents, value); }
    public string HealthStatus { get => healthStatus; private set => SetProperty(ref healthStatus, value); }
    public DesktopPage CurrentPage => current.Page;
    public string CurrentTitle => current.Title;
    public string CurrentDescription => current.Description;
    private void Navigate(DesktopPage page) { var next = NavigationItems.Single(item => item.Page == page); if (next == current) return; current = next; RaisePropertyChanged(nameof(CurrentPage)); RaisePropertyChanged(nameof(CurrentTitle)); RaisePropertyChanged(nameof(CurrentDescription)); }
    public async Task RefreshHealthAsync()
    {
        if (healthService is null) { HealthStatus = "健康服务未配置"; return; }
        HealthStatus = "正在检查本机状态…";
        var snapshot = await healthService.CheckAsync();
        HealthComponents = snapshot.Components;
        HealthStatus = snapshot.Overall switch { HealthLevel.Healthy => "全部正常", HealthLevel.Warning => "部分功能待就绪", _ => "存在不可用组件" };
    }
}
