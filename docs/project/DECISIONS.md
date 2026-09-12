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
| 2026-09-10 | Windows 候选版采用自包含 ZIP，不捆绑 mpv/FFmpeg 或用户数据 | [ADR-0011](../adr/ADR-0011-windows-zip-distribution.md)，已被 ADR-0013 取代 |
| 2026-09-10 | 根目录 `common` 仅存本机私有媒体工具副本，优先于 PATH；二进制保持忽略且不改变正式 ZIP 分发边界 | Accepted（KTVS-060） |
| 2026-09-10 | WPF 主控以用户确认的 HTML Demo 为最低视觉基线，统一主题、导航、卡片和关键操作层级 | Accepted（KTVS-061） |
| 2026-09-11 | 扫描采用基础索引与可恢复探测两阶段；CloudDrive 默认单探测，本地可配置 2–4 | [ADR-0012](../adr/ADR-0012-two-stage-scanning.md) |
| 2026-09-12 | 发布 ZIP 携带经版本核对的 mpv、FFmpeg/ffprobe 和许可证说明，程序自动定位且不要求用户填写路径 | [ADR-0013](../adr/ADR-0013-bundled-media-tools.md) |
| 2026-09-12 | 播放控制台的音量滑动采用短防抖自动提交；播放标题/歌手由当前队列公开 DTO 补齐，轨道没有标题时显示稳定的本地回退名称 | Accepted（KTVS-073） |
| 2026-09-12 | `ProbeFailed` 不阻塞队首：后台预探测保留失败状态，播放存储在队首再次尝试媒体；终态队列位置不复用，避免唯一键冲突 | Accepted（KTVS-074） |
| 2026-09-12 | 手机搜索输入与提交查询分离，点歌页状态按房间/访客保存在当前标签页；歌星浏览与歌曲结果严格互斥 | Accepted（KTVS-075） |
| 2026-09-12 | “按歌星”主入口直接加载全部歌手卡片；歌手分组按钮留在卡片视图内，切换分组只刷新歌手列表，不回退到歌曲结果 | Accepted（KTVS-077） |
| 2026-09-12 | 队列插歌/重排位置统一分配到全房间历史最小位置之前，避开已播放终态记录的唯一键；WPF 队列命令捕获异常并显示可恢复提示，手机端串行化插歌请求 | Accepted（KTVS-078） |

重大、跨模块的决定在 `docs/adr/` 单独记录；实现变更必须同步更新或 Supersede 对应 ADR。
