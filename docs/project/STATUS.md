# 项目状态

更新时间：2026-09-08

## 当前基线

- 分支：`task-KTVS-001-002-phase0`
- 版本：`0.1.0-dev`
- 阶段：Phase 4：房间与队列后端
- 已完成：KTVS-001 至 KTVS-032
- 当前任务：KTVS-033 历史、收藏和热门统计

## 调查结果

- 初始目录仅有权威开发计划；未发现其他 `AGENTS.md` 或状态文档。
- Git 已初始化为独立仓库；保留原计划文件，未覆盖用户文件。
- Node.js `v24.18.0`、npm `11.16.0` 可用。
- `ffprobe`/`ffmpeg` 可用（来自 `D:\Applications\ffmpeg\bin`，未写入项目配置）。
- .NET SDK `10.0.400`（绝对路径可用）；mpv 已由 WinGet 安装并可定位。

## 最近验证

已执行：`pwsh.exe -ExecutionPolicy Bypass -File scripts/verify-environment.ps1`；结果为 .NET `10.0.400`、Node `v24.18.0`、npm `11.16.0`、FFmpeg/ffprobe `8.1.2`、mpv `v0.41.0-dev-g41f6a6450`，夹具 `10.023s`、四轨与中文标题正确、`END_FILE=received`、`ENVIRONMENT_BASELINE=passed`。真实设备项仍未验证。

KTVS-006：`dotnet build AI-KTV-Station.slnx --configuration Release -m:1 -p:UseSharedCompilation=false` 成功，0 警告/0 错误；依赖检查输出 `PROJECT_DEPENDENCIES=passed`。沙箱内并行 MSBuild 会遇到命名管道权限限制，基线命令暂用 `-m:1` 与禁用共享编译。

KTVS-007：xUnit 测试 10/10 通过；Release 解决方案构建 0 警告/0 错误。测试覆盖 Result/Error、配置验证和敏感日志属性脱敏。NuGet 版本集中管理并生成锁文件。

KTVS-008：统一测试脚本 10/10 通过，Cobertura line-rate `0.9677`；测试数据工厂和锁文件已纳入基线。已清理并忽略误提交的 `tests/**/bin`、`obj` 与 `TestResults`。

KTVS-009：本地执行 locked restore、format verify、Release build 和 coverage test 全部通过；构建 0 警告/0 错误，测试 10/10。GitHub Actions 工作流尚未推送运行，保持待外部验证。

KTVS-010：`bootstrap.ps1`、`build.ps1`、`test.ps1` 与 Server smoke 连续通过；输出 `BOOTSTRAP=passed`、`BUILD=passed`、10/10 测试及 `SERVER_SMOKE=passed`。

KTVS-011：Release 构建 0 警告/错误；11/11 测试通过，其中临时 SQLite 执行 `InitialCreate` 并往返保存离线 Song/MediaFile；EF 工具列出 `20260903142705_InitialCreate (Pending)`。

KTVS-012：Release 构建 0 警告/错误；13/13 测试通过。覆盖多根临时目录、启停、缺失/重复路径拒绝，以及公共 DTO 不含路径属性。

KTVS-013：16/16 测试通过。临时 Unicode 目录验证只读枚举、大小写 MKV、重复扫描零更新、缺失文件仅标记 Offline、局部错误保守保留状态，以及取消后保存检查点；新增 `AddScanCheckpoint` 迁移。

KTVS-014：22/22 测试通过。解析计划中的三类文件名，并覆盖全角连接符/括号、标题连字符、多歌手候选、质量/语言/类别/版本标签及低置信度回退；扫描新增文件使用解析结果建占位元数据。

KTVS-015：默认测试 28/28、真实 ffprobe 生成夹具测试 1/1 通过。约 10 秒 Unicode MKV 探测得到 `10.023s`、四条视频/双音频/字幕轨和中文标题；扫描器仅对新增/变化文件探测并持久化轨道，失败标记 `Unreadable` 且不删除索引。

KTVS-016：默认测试 32/32 通过。安全 XML 读取覆盖 Unicode、缺失、malformed 与 DTD/外部实体拒绝；扫描入库采用可选 NFO，并通过测试确认人工修正 > NFO > 文件名的逐字段优先级及不一致 warning。

KTVS-017：默认测试 36/36 通过。新增简体、繁体、全拼、简拼、原文规范化和紧凑字段及 EF 迁移；Unicode/全角/空白/emoji、简繁中文和扫描补填均有回归测试。ToolGood.Words `3.1.0.3` 的 Apache-2.0 包元数据、源码提交和 lock file 已核对记录。

KTVS-018：默认测试 38/38、独立性能测试 1/1 通过。FTS5 覆盖简繁、标题、歌手、拼音/简拼、分页、排序、筛选、更新/删除同步、特殊查询和公共 DTO 路径隔离；100k 混合查询 30 样本 P95 `4.74ms`、最大 `5.03ms`。

KTVS-019：默认测试 43/43 通过，Server smoke 通过。后台协调器使用独立 scope，覆盖创建、实时计数、同源去重、定向取消、终态、重启后持久化结果回读及完成后 FTS rebuild；API 契约反射确认不含路径字段。

KTVS-020：默认测试 48/48 通过。Application 层正式定义 `IPlayerAdapter` 的启停、加载、播放控制、状态读取和异步事件流，以及播放实例/事件关联 ID、结束原因、可重试错误分类和参数验证；程序集引用检查确认不依赖 Infrastructure 或 mpv。

KTVS-021：默认测试 49/49、mpv 外部测试 2/2 通过。正式 `MpvPlayerAdapter` 使用每进程唯一 Windows named pipe、递增 `request_id`、单写/持续读循环和命令超时；生成 Unicode MKV 覆盖完整播放、四轨读取、原唱/伴奏和字幕切换、EOF，以及自有 mpv 进程意外退出分类。真实设备仍待验收。

KTVS-022：默认测试 55/55 通过。Application 层纯状态机覆盖合法主路径、非法迁移拒绝、播放实例替换后的旧事件、事件 ID 幂等、时间乱序和可重试故障保留；不依赖 mpv 或 Infrastructure。

KTVS-023：默认测试 62/62 通过。播放控制服务集中验证音量、活动播放、定位时长边界和字幕轨类型；进度 DTO 不含路径，成功命令返回适配器同步快照，稳定错误码不作协议泄漏式重写。真实字幕显示和功放音量待实机验收。

KTVS-024：默认测试 69/69 通过。按中英文标题生成带置信度的原唱/伴奏候选，高置信结果随扫描自动持久化；无标题双轨不武断自动映射，人工覆盖在重扫时受保护且解析优先。另修复已有媒体重探测时新轨道的 EF 状态识别。真实声道语义待实机验收。

KTVS-025：默认测试 79/79 通过。Application 恢复策略提供有限指数退避、按错误分类重启播放器、耗尽后自动跳过及系统性故障停止；所有故障先持久化，媒体离线/不可读只更新状态和错误码，不删除索引，并保留仍有在线媒体版本的歌曲可用状态。真实 CloudDrive 403/断挂待实机验收。

KTVS-026：生成 MKV 的 50 轮连续加载/切轨/字幕/定位/EOF 测试 50/50 通过，重复完成事件 0、mpv 重启 0、耗时 32.339 秒；修复 EOF 后状态回退为 Playing 及终态 Stop/下一次 Load 重复结束事件风险。工作集从约 33.8 MB 增至 107.2 MB，列入长时实机观察。真实 50 首、电视和功放仍待验收。

KTVS-027：房间服务覆盖单一开放房间、队列限额、加入码冲突重试、幂等关闭、当前房间读取和 SQLite 跨上下文重启恢复；公开 DTO 不包含令牌哈希或媒体信息。

KTVS-028：短期房间令牌采用 32 字节密码学随机数且仅持久化 SHA-256 哈希；访客/主持人分别为 12/8 小时，覆盖到期、撤销、关闭房间、角色隔离和服务端权限矩阵。默认测试新增 6 个场景；二维码及手机重连待实机验收。

KTVS-029：队列服务覆盖可用歌曲追加、访客活动点歌额度、本人删除、主持人任意删除/置顶、终态保留及公开无路径 DTO；同房间写入由进程内锁串行化，20 路并发测试无重复位置且排序稳定。

KTVS-030：播放编排器通过 `IPlayerAdapter` 串联队列准备/播放/完成、播放历史、故障记录、有限重试、mpv 重启、跳过和自动下一首；重启时更换播放实例 ID 以隔离旧结束事件。5 个自动场景覆盖双曲连播、恢复与系统性停止，真实设备仍待验收。

KTVS-031：Minimal API 提供本机房间管理、访客加入、曲库搜索、队列和播放控制，全部业务变更重新验证短期房间令牌与角色；OpenAPI 位于 `/openapi/v1.json`。端到端测试覆盖房间→加入→搜索→点歌→置顶/删除→播放控制→关闭，并确认 JSON/OpenAPI 不含路径和令牌哈希。

KTVS-032：`/hubs/room` 连接后通过 Hub 方法验证短期令牌，避免令牌进入 URL 日志；每房间单调版本和 256 条事件窗口支持在线推送、重连增量补发及版本缺口完整快照。真实 SignalR 客户端测试覆盖在线/离线恢复和无效令牌拒绝；WebSocket 手机实机待验收。

## 外部阻塞

1. 真实 MKV、挂载目录和电视/功放/手机验收需用户后续执行，不阻塞软件开发。

## 用户实机验收（待验证）

尚未进行。后续需用户在 Windows 11 上确认：mpv 画面、双音轨语义、字幕、手机扫码入房、CloudDrive 挂载速度及音频输出。

## 下一推荐任务

执行 `KTVS-033`：实现播放历史、访客收藏和热门统计。
