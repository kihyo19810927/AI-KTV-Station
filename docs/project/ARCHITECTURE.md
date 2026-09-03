# 工程架构

## 项目职责

| 项目 | 职责 | 允许的项目依赖 |
|---|---|---|
| `Station.Domain` | 实体、值对象、领域规则和事件 | 无 |
| `Station.Application` | 用例、端口、DTO 和编排 | Domain |
| `Station.Infrastructure` | EF Core、文件扫描、ffprobe、mpv 等端口实现 | Application |
| `Station.Server` | Minimal API、SignalR、Web 静态宿主 | Application、Infrastructure |
| `Station.Desktop` | WPF/MVVM 主控和进程组合根 | Application、Infrastructure |
| `Station.Web` | React/Vite 手机 SPA（KTVS-034 建立） | 仅 HTTP/SignalR 契约 |
| `Station.PlayerSpike` | Phase 0 可行性验证，不作为生产业务入口 | 无 |

依赖方向由 `scripts/verify-project-dependencies.ps1` 自动检查。当前 WPF 与 Server 分别可构建；KTVS-041 将 WPF 组合根接入内嵌 Server Host。
