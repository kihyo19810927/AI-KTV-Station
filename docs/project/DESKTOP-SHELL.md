# WPF 主控应用壳

KTVS-041 将已有占位 `Station.Desktop` 建成 Windows 11 WPF/MVVM 主控壳。

## 组合根

`App` 是唯一 UI 组合根，使用 `Microsoft.Extensions.DependencyInjection` 注册窗口和 ViewModel；启动时启用作用域/构造验证，退出时释放容器。窗口构造函数只接收 `MainWindowViewModel`，code-behind 不包含业务逻辑。

## 导航

左侧固定导航对应 V1 六个工作区：仪表盘、正在播放、点歌队列、曲库管理、房间与二维码、设置与诊断。`MainWindowViewModel` 暴露只读导航项、当前页面和命令，页面变化通过 `INotifyPropertyChanged` 驱动标题/说明。

主题资源集中在 `App.xaml`，使用与手机 Demo 一致的紫色强调、浅色工作区和深色导航。后续页面通过 ViewModel/服务接口接入 Application 能力，不把 API、EF 或 mpv 调用写入窗口 code-behind。

## 验证

独立 `Station.Desktop.Tests`（`net10.0-windows`）验证六个导航目标和属性通知。Release 构建验证 XAML 编译与 DI 引用；窗口视觉、缩放、多屏和电视扩展屏仍待 Windows 实机验收。
