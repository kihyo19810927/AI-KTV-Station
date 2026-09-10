# 决策索引

| 日期 | 决策 | 状态 |
|---|---|---|
| 2026-09-03 | Station 是独立仓库，仅通过成品媒体文件边界与 AI-KTV-Builder 协作 | Accepted（计划已确定） |
| 2026-09-03 | 首版播放器目标为 mpv 子进程 + JSON IPC，并由 `IPlayerAdapter` 隔离 | Accepted（计划已确定） |
| 2026-09-03 | 首版 Web API 不转发视频；手机不获取真实路径或永久管理员密钥 | Accepted（计划已确定） |
| 2026-09-03 | 依赖方向为 UI/Infrastructure → Application → Domain，V1 单进程优先 | [ADR-0002](../adr/ADR-0002-repository-and-dependency-boundaries.md) |
| 2026-09-03 | 播放器经 `IPlayerAdapter` 隔离，mpv 使用 named-pipe JSON IPC | [ADR-0003](../adr/ADR-0003-player-adapter-and-mpv-ipc.md) |
| 2026-09-03 | EF Core + SQLite/FTS5，歌曲身份与媒体在线状态分离 | [ADR-0004](../adr/ADR-0004-sqlite-persistence-and-search.md) |
| 2026-09-03 | React/Vite 静态产物由 ASP.NET Core 托管 | [ADR-0005](../adr/ADR-0005-web-build-and-hosting.md) |
| 2026-09-03 | 短期、限定房间/角色的令牌；默认仅局域网暴露 | [ADR-0006](../adr/ADR-0006-room-authentication.md) |
| 2026-09-07 | 歌曲字段按人工修正、NFO、文件名逐字段合并；NFO 异常或不一致不阻止入库 | Accepted（KTVS-016） |
| 2026-09-07 | 搜索规范化经应用端口隔离，Infrastructure 使用 ToolGood.Words 预计算简繁、拼音和简拼键 | [ADR-0007](../adr/ADR-0007-search-text-normalization.md) |
| 2026-09-07 | 公开搜索文档表由 trigger 同步 external-content FTS5，查询不访问媒体路径 | [ADR-0008](../adr/ADR-0008-fts5-search-index.md) |
| 2026-09-08 | 单次播放器故障与业务恢复分离；有限重试、错误留痕、离线不删索引 | [ADR-0009](../adr/ADR-0009-playback-failure-recovery.md) |
| 2026-09-10 | 主控二维码使用 MIT QRCoder 1.8.0，仅编码局域网 URL 与房间码 | Accepted（KTVS-046） |
| 2026-09-10 | WPF 内嵌 ASP.NET/SignalR 并共享单一播放器；后台协调唯一开放房间 | [ADR-0010](../adr/ADR-0010-embedded-station-host.md) |

重大、跨模块的决定在 `docs/adr/` 单独记录；实现变更必须同步更新或 Supersede 对应 ADR。
