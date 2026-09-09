# 测试矩阵

| 范围 | 方法 | 当前状态 | 阻塞 |
|---|---|---|---|
| 文档边界/术语 | 人工审阅、链接检查 | 可执行 | 无 |
| 项目依赖方向 | `scripts/verify-project-dependencies.ps1` | 通过 | 无 |
| .NET 解决方案构建 | Release、单节点、禁用共享编译 | 通过（6 项目，0 警告/错误） | 并行编译受当前沙箱管道权限限制 |
| Result/配置/日志脱敏 | xUnit | 通过（10/10） | 无 |
| .NET 领域单元测试 | xUnit | 基线已建立 | 领域功能随任务补充 |
| 覆盖率 | coverlet + Cobertura | line-rate 0.9677 | 当前仅基线代码，不代表后续功能覆盖率 |
| CI 命令链 | locked restore + format + build + test | 本地通过 | GitHub Actions runner 待首次推送验证 |
| Server 启动 | 本地进程 + `/health` | 通过 | 无 |
| EF Core SQLite 迁移 | 随机临时数据库 + `MigrateAsync` | 通过（InitialCreate） | 无 |
| 领域持久化 | Song/MediaFile 离线状态往返 | 通过 | 无 |
| MediaSource 管理 | 临时 Unicode 多目录 + SQLite | 通过（添加/启停/缺失/重复） | 真实 CloudDrive 待验收 |
| API 路径泄漏 | 公共 DTO 反射断言 | 通过（无 Path 属性） | 后续 API 契约仍需复测 |
| 只读增量扫描 | 临时 Unicode 目录 | 通过（增量、离线保留、错误隔离） | 真实 CloudDrive 待验收 |
| 扫描取消/恢复 | 受控异步枚举器 | 通过（Cancelled + checkpoint） | 后续恢复入口仍需 API 测试 |
| 文件名解析 | 计划样本 + Unicode/容错回归 | 通过（9 个断言场景） | 真实脱敏命名样本后补 |
| Host/API/SignalR | 集成测试 | 未开始 | .NET SDK |
| mpv IPC | 生成夹具 + Windows named-pipe 实机进程 | 软件 Spike 通过 | 真实 MKV/设备待验收 |
| ffprobe JSON/失败分类 | 固定 JSON、缺失可执行文件、扫描器替身 | 通过 | 无 |
| ffprobe 真实进程 | `scripts/test-media-probe.ps1` + 自动生成 Unicode MKV | 通过（10.023s、4 轨） | 真实挂载媒体待验收 |
| NFO 元数据 | Unicode 临时 XML、缺失/malformed/DTD、来源合并与扫描入库 | 通过（4 个场景） | 真实 Builder 脱敏 NFO 后补 |
| 搜索规范化 | Unicode NFKC、空白/紧凑键、简繁、拼音/简拼、旧索引补填 | 通过（4 个场景） | 多音词真实样本后补 |
| FTS5 搜索契约 | SQLite 迁移、简繁/拼音/简拼、分页/排序/筛选、同步与路径反射 | 通过（2 个集成场景） | 无 |
| FTS5 100k 性能 | `scripts/test-search-performance.ps1`，30 次混合查询 | 通过（P95 4.74ms，max 5.03ms） | 300k/低规格 Windows 后补 |
| 扫描协调器 | 独立 DI scope、进度、同源去重、取消、FTS rebuild | 通过（2 个并发场景） | 无 |
| 扫描管理 API | TestServer 创建/查询/取消/结果、真实空目录扫描、历史回读、路径反射 | 通过（3 个集成场景） | 认证随 Phase 5；真实挂载待验收 |
| 播放器端口契约 | 反射、程序集依赖、参数错误和 DTO 路径/协议泄漏检查 | 通过（5 个场景） | 无 |
| mpv 正式适配器 | 缺失可执行文件；生成 Unicode MKV 完整播放/轨道切换/EOF；自有进程意外退出 | 通过（默认 1、外部 2） | 真实 MKV/电视/功放待验收 |
| 播放状态机 | 合法主路径、非法迁移、替换后旧事件、重复/乱序事件、失败分类 | 通过（6 个场景） | 无 |
| 音量/进度/字幕控制 | 参数短路、活动状态、时长边界、字幕类型、关闭字幕、错误透传 | 通过（7 个场景） | 电视字幕和功放音量待实机验收 |
| 原唱/伴奏映射 | 中英文标题、低置信歧义、自动落库、人工优先、重扫保护、非法覆盖、SQLite 往返 | 通过（8 个场景） | 真实曲库声道语义待验收 |
| 播放故障恢复 | 退避/耗尽、进程重启分类、跳过/停止、错误净化、非法次数、SQLite 保留索引 | 通过（10 个场景） | 真实 CloudDrive 403/断挂待验收 |
| 播放器连续耐久 | `scripts/test-player-endurance.ps1 -Cycles 50`，同一 mpv 循环加载/切轨/字幕/定位/EOF | 通过（50/50、重复结束 0、重启 0、32.339s） | 真实 50 首/电视/功放待验收；工作集增长待长测 |
| 房间生命周期 | 创建/单房间、队列上限、加入码冲突、幂等关闭、SQLite 重启恢复 | 通过（6 个场景） | 无 |
| 房间认证与授权 | 随机令牌、哈希落库、访客/主持人、到期、撤销、关闭房间、权限矩阵 | 通过（6 个场景） | 二维码/手机重连待实机验收 |
| 点歌队列规则 | 追加/额度、本人和主持人删除、置顶、20 路并发稳定排序、离线媒体拒绝 | 通过（6 个场景） | 无 |
| 队列播放编排 | 双曲自动连播、进程重启重试、耗尽跳过、系统性停止、迟到事件隔离 | 通过（5 个场景） | 真实电视/功放/CloudDrive 待实机验收 |
| REST API/OpenAPI | 房间创建/加入、Bearer 认证、搜索、队列、主持人权限、播放控制、RFC 7807、Schema 路径隔离 | 通过（2 个端到端场景） | 局域网手机待实机验收 |
| SignalR 房间同步 | 在线增量、断线补发、256 事件窗口缺口、完整快照、无效令牌拒绝 | 通过（3 个场景，真实客户端 Long Polling） | WebSocket/手机网络切换待实机验收 |
| 收藏/历史/热门 | 幂等收藏、访客隔离、历史房间分页、成功播放聚合、参数验证、REST 契约 | 通过（4 个服务场景 + API 端到端） | 无 |
| React Web 基线 | TypeScript、Vitest、Vite 生产构建、ASP.NET 静态根页/SPA 深链 | 通过（4 个前端场景 + 2 个托管路径） | Android/iPhone 后验收 |
| Web 加入与会话恢复 | Vitest + jsdom，真实 API 客户端请求替身 | 通过（9 个前端场景：加入、预填、恢复、退出、过期/损坏清理） | 二维码与 Android/iPhone 标签页生命周期后验收 |
| Web 曲库搜索 | Vitest + API 请求替身 | 通过（防抖查询、筛选参数、分页追加、空状态、错误重试、离线禁用） | 大曲库滚动与手机软键盘后验收 |
| Web 点歌与我的歌曲 | Vitest + API 请求替身 | 通过（成功点歌、重复拦截、额度提示、本人筛选、等待项删除） | 多手机并发交互随实时同步后验收 |
| Web SignalR 实时状态 | Vitest reducer/Hub 替身 + 服务端真实 SignalR 客户端 | 通过（版本幂等、增量、快照、播放卡片、令牌不进 URL；服务端在线/补发已覆盖） | 手机 WebSocket、Wi-Fi 切换和休眠恢复后验收 |
| Web 收藏/热门/最近新增 | Vitest + SQLite 搜索回归 | 通过（收藏切换/列表、热门入口、最近新增参数、AddedAt 迁移与稳定排序） | 多访客手机交互后验收 |
| 手机 Web 兼容基线 | Vitest 静态契约 + Vite 多浏览器目标 | 通过（320px、44px 触摸、焦点、safe-area、viewport、Chrome/Edge/Safari 构建目标） | Android Chrome 与 iPhone Safari 后验收 |
| WPF 主控壳 | net10.0-windows xUnit + Release XAML 构建 | 通过（六目标导航、命令和属性通知 2 个场景） | Windows 缩放、多屏和视觉后验收 |
| WPF 健康仪表盘 | xUnit + SQLite 内存库 + ViewModel 替身 | 通过（组件汇总、安全摘要、刷新状态） | CloudDrive/115 实挂待验收 |
| WPF 播放控制台 | xUnit 播放器替身 + Release XAML 编译 | 通过（状态/进度、轨道分组、控制命令） | 真实 MKV/电视/功放待验收 |
| WPF 队列管理 | xUnit + SQLite 唯一索引 + Release XAML 编译 | 通过（刷新、授权重排、事务换位、空房间） | 拖放手感待 Windows 实机验收 |
| 环境与生成 MKV | `scripts/verify-environment.ps1` | 通过（10.023s、4 轨、中文标题） | 无 |
| 真实 MKV/设备 | `ENVIRONMENT-BASELINE.md` 用户验收步骤 | 待验证 | 用户设备、样本和挂载 |

禁止把替身、模拟器或文档审阅结果写成真实设备通过。
