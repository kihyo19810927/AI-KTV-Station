# ADR-0010：WPF 内嵌 Station 服务与单一播放协调器

- 状态：Accepted
- 日期：2026-09-10

## 背景

WPF 主控、ASP.NET Core API/SignalR 和播放器此前可分别启动，但主控生成的二维码没有对应的同进程服务生命周期，服务端队列也不会在房间空闲时持续发现后续新增歌曲。UAT 必须验证一个可直接运行的主控闭环。

## 决策

提取 `StationServerHost` 作为 Server 与 Desktop 共用的组合根。Desktop 启动同进程 Kestrel，使用相同数据目录并注入同一个 `IPlayerAdapter`。服务端只注册一个 `RoomPlaybackHostedService`：它跟踪唯一开放房间，由 `QueuePlaybackOrchestrator` 消费播放器事件并轮询空闲队列，房间关闭或主机退出时取消工作。

播放器协议仍只存在于 Infrastructure 的 mpv 适配器中；API、WPF 和后台服务均依赖 `IPlayerAdapter`。公开接口不返回路径。Desktop 拥有外部创建的播放器实例和最终释放责任。

## 后果

- 运行 Desktop 即可同时提供手机 Web、REST、SignalR 和本机播放。
- Server 仍可独立运行，便于测试与诊断。
- SQLite 会被桌面与服务端多个 DbContext 并发访问，需继续采用短作用域和迁移前备份。
- 真实局域网、电视、功放和手机生命周期仍需实机验收。
