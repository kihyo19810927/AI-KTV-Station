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
| FTS5 搜索契约 | SQLite 迁移、简繁/拼音/简拼、分页/排序/歌星分组筛选、同步与路径反射 | 通过（3 个集成场景） | 无 |
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
| 队列预探测与 mpv idle 窗口 | EF 等待队列 + 真实 mpv Unicode MKV | 通过（按队列位置单线程预探测；真实 mpv 2/2，EOF 后进程 ID 不变） | 真实 NAS 多曲队列待验收 |
| 房间生命周期 | 创建/单房间、队列上限、加入码冲突、幂等关闭、SQLite 重启恢复 | 通过（6 个场景） | 无 |
| 房间认证与授权 | 随机令牌、哈希落库、访客/主持人、到期、撤销、关闭房间、权限矩阵 | 通过（6 个场景） | 二维码/手机重连待实机验收 |
| 点歌队列规则 | 追加/额度、本人和主持人删除、置顶、20 路并发稳定排序、离线媒体拒绝 | 通过（6 个场景） | 无 |
| 队列播放编排 | 双曲自动连播、队首按需探测和缓存、进程重启重试、耗尽跳过、系统性停止、迟到事件隔离 | 通过（6 个场景） | 真实电视/功放/CloudDrive 待实机验收 |
| REST API/OpenAPI | 房间创建/加入、Bearer 认证、搜索、队列、主持人权限、播放控制、RFC 7807、Schema 路径隔离 | 通过（2 个端到端场景） | 局域网手机待实机验收 |
| SignalR 房间同步 | 在线增量、断线补发、256 事件窗口缺口、完整快照、无效令牌拒绝 | 通过（3 个场景，真实客户端 Long Polling） | WebSocket/手机网络切换待实机验收 |
| 收藏/历史/热门 | 幂等收藏、访客隔离、历史房间分页、成功播放聚合、参数验证、REST 契约 | 通过（4 个服务场景 + API 端到端） | 无 |
| React Web 基线 | TypeScript、Vitest、Vite 生产构建、ASP.NET 静态根页/SPA 深链 | 通过（4 个前端场景 + 2 个托管路径） | Android/iPhone 后验收 |
| Web 加入与会话恢复 | Vitest + jsdom，真实 API 客户端请求替身 | 通过（9 个前端场景：加入、预填、恢复、退出、过期/损坏清理） | 二维码与 Android/iPhone 标签页生命周期后验收 |
| Web 曲库搜索 | Vitest + API 请求替身 | 通过（防抖查询、筛选参数、分页追加、空状态、错误重试、离线禁用） | 大曲库滚动与手机软键盘后验收 |
| Web 点歌与我的歌曲 | Vitest + API 请求替身 | 通过（成功点歌、重复拦截、额度提示、本人筛选、等待项删除） | 多手机并发交互随实时同步后验收 |
| Web 播放标签与即时队列 | Vitest + SignalR/API 替身 | 通过（25/25；独立播放控制页、无“我的”入口、POST 成功后无需刷新即可查看已点） | 真实手机 WebSocket 事件时序待验收 |
| Web SignalR 实时状态 | Vitest reducer/Hub 替身 + 服务端真实 SignalR 客户端 | 通过（版本幂等、增量、快照、播放卡片、令牌不进 URL；服务端在线/补发已覆盖） | 手机 WebSocket、Wi-Fi 切换和休眠恢复后验收 |
| Web 收藏与分类发现 | Vitest + SQLite 搜索回归 | 通过（收藏切换/列表；歌星、语种、风格独立二级菜单；同名歌手聚合；无组合下拉框） | 歌手分组未知数据归“其他”；歌手图片刮削和手机交互后验收 |
| 手机 Web 兼容基线 | Vitest 静态契约 + Vite 多浏览器目标 | 通过（320px、44px 触摸、焦点、safe-area、viewport、Chrome/Edge/Safari 构建目标） | Android Chrome 与 iPhone Safari 后验收 |
| WPF 主控壳 | net10.0-windows xUnit + Release XAML 构建 | 通过（六目标导航、命令和属性通知 2 个场景） | Windows 缩放、多屏和视觉后验收 |
| WPF 健康仪表盘 | xUnit + SQLite 内存库 + ViewModel 替身 | 通过（组件汇总、安全摘要、刷新状态） | CloudDrive/115 实挂待验收 |
| WPF 播放控制台 | xUnit 播放器替身 + Release XAML 编译 | 通过（状态/进度、轨道分组、控制命令） | 真实 MKV/电视/功放待验收 |
| WPF 队列管理 | xUnit + SQLite 唯一索引 + Release XAML 编译 | 通过（刷新、授权重排、事务换位、空房间） | 拖放手感待 Windows 实机验收 |
| 多格式/辅助文件扫描 | 临时 MPG/MPEG/MKV/KSC/RAR + SQLite | 通过（视频入库、KSC 关联、RAR 忽略、无 NFO 文件名建库） | 小批真实 115 样本待验收 |
| 分目录增量建库 | 两个年度临时来源依次扫描 | 通过（先入库可用、后续追加、既有来源保持可用） | 全库规模随 KTVS-049 |
| 保留式数据库升级 | 上一迁移预置数据 → 最新迁移 | 通过（歌曲、收藏、历史、人工音轨映射保留） | 正式库升级前仍需备份演练 |
| WPF 曲库管理 | xUnit + 临时 SQLite/Unicode JSONL + Release XAML 编译 | 通过（camelCase artist、MPG、FTS、重复导入 0 新增；界面仅保留 JSON/JSONL 增量导入） | 72,295 首索引、真实挂载根目录和导入耗时待实机验收 |
| WPF 房间与二维码 | SQLite + QRCoder PNG + ViewModel | 通过（房间、规则、访客公开投影、无令牌 URL/二维码） | 手机扫码、网卡选择和 Wi-Fi 连通待验收 |
| 设置、日志与诊断 | 临时 JSON 设置、JSONL 日志、健康替身、WPF ViewModel | 通过（配置验证/往返、最近事件、换行净化、路径脱敏、恢复建议、重启提示） | 实际导出目录可用性随 Windows 验收 |
| 关键业务端到端 | Unicode MPG/KSC 临时目录 + SQLite + TestServer + 可控播放器端口 | 通过（扫描、FTS、建房、加入、点歌、媒体加载、完成队列与历史） | 真实 115/mpv/电视/功放/手机不由替身结论覆盖 |
| 100k/300k 大曲库性能 | 合成 SQLite FTS 文档 + 流式扫描条目 | 通过（搜索 P95 4.18/12.54 ms；扫描 0.51/1.99 s） | 扫描数字不含文件系统、ffprobe 和 EF 写盘；低规格机/真实挂载待验收 |
| CloudDrive 故障注入 | 临时 SQLite/媒体 + 错误枚举器 + 可控播放器 | 通过（断挂不误删、403 离线保留、重扫恢复、超时重启并完成） | 真实 115/CloudDrive 错误文本和时延待验收 |
| 进程与启动恢复 | SQLite 文件重开 + 三种活动队列状态 + 真实 mpv 进程终止 | 通过（重入队、旧历史闭合、幂等恢复、mpv 可重试崩溃事件） | 突然断电与磁盘写缓存待实机验收 |
| 安全边界 | 配置验证、loopback 策略、TestServer、OpenAPI、脱敏和 Web 存储测试 | 通过（显式 IP、仅本机管理、无路径/哈希契约、安全头、令牌不进 URL/localStorage） | LAN HTTP 被动监听风险；严禁公网暴露 |
| 依赖与许可证 | NuGet/NPM 锁文件、已还原包元数据、ffmpeg buildconf、mpv version、随包 notices | 进行中（工具版本和来源说明已加入；完整许可证材料需发布前复核） | 项目自身许可、公开二进制分发仍需所有者复核 |
| Desktop 内嵌服务与后台播放 | 真实 Kestrel 临时端口、共享播放器 DI、空队列后追加歌曲 | 通过（主机健康、单播放器、延迟点歌自动加载） | LAN 手机与真实媒体待实机验收 |
| UAT 软件就绪包 | `scripts/run-uat-readiness.ps1` | 通过（Core 150/150、Desktop 12/12、Web 24/24、生成 MKV/mpv） | 用户执行设备与小批真实库步骤 |
| Windows 自包含发布包 | `scripts/publish-windows.ps1` + `verify-release-package.ps1` + `test-packaged-app.ps1` | RC12 通过（579 项、mpv/ffmpeg/ffprobe/Vulkan loader、许可证说明；隔离启动/健康/窗口/退出清理通过；无数据库/设置） | 干净 Windows 安装、工具版本和正式许可证复核待验收 |
| 数据库安全升级 | 临时 SQLite + 可控迁移执行器 | 通过（无迁移不备份、备份完整、删除数据后失败自动恢复） | 正式库升级与磁盘故障待实机演练 |
| 运维文档完整性 | `scripts/verify-operations-docs.ps1` | 通过（6 份文档、7 章节、3 条安全警告） | 操作可用性随 KTVS-058 实机验收 |
| RC6 候选版软件验证 | UAT 就绪链 + 发布包验证 + 随机临时目录启动/退出 | 通过（Core 159、Desktop 14、Web 24、566 项 ZIP、SQLite/内嵌服务健康、进程清理） | 干净 Windows 与真实设备仍待验收 |
| 1.0.0 发布材料 | `scripts/verify-release-readiness.ps1` | 通过（Release Notes 五章节、候选记录版本、ZIP/sidecar/文档 SHA-256 一致、Tag 明确待创建） | 正式 Tag/Release 受实机、许可和所有者授权阻塞 |
| 扫描协调器资源释放 | scope 阻塞释放、活动扫描关闭取消、端到端临时 SQLite 清理 | 通过（终态晚于 scope 释放；关闭等待任务；不残留数据库文件锁） | 真实长时扫描中关闭应用待实机观察 |
| GitHub Actions 发布前兼容 | Windows runner push workflow | #8 通过（3m02s、coverage artifact、无 Annotation）；Node 24 的 v5/v7 Action 组合无弃用警告 | #7 聚合测试瞬态失败已保留审计记录 |
| 环境与生成 MKV | `scripts/verify-environment.ps1` | 通过（10.023s、4 轨、中文标题） | 无 |
| 真实 MKV/设备 | `ENVIRONMENT-BASELINE.md` 用户验收步骤 | 待验证 | 用户设备、样本和挂载 |
| 本机 `common` 工具发现 | xUnit 临时层级 + common 中真实 mpv/ffprobe 外部测试 | 通过（定位 2/2、外部媒体 4/4） | 二进制仅本机，不作为可分发包结论 |
| WPF Demo 视觉基线与曲库入口 | Release XAML 编译 + ViewModel xUnit | 通过（Desktop 13/13；未选来源时扫描禁用） | 色彩、密度与大屏效果待用户目视验收 |
| 两阶段扫描与真实十首样本 | xUnit + 限定目录 ffprobe | 通过（指纹跳过、取消续扫；远程并发1 60.03s/10成功，并发2 106.55s/5成功；本地0.68s/0.33s） | 仅十首样本，不代表全库完成时间 |
| JSON 曲库保留式导入 | 36,601 条附件索引 + SQLite 备份 + FTS 重建 | 通过（36,601 导入、0 跳过；正式库导入前备份） | 路径只按相对路径拼接，媒体未读取或修改 |
| RC6 进程清理 | 全新解压启动、正常关闭、进程与端口复核 | 通过（`PACKAGE_PROCESS_CLEANUP=passed`；Station 0、mpv 0、5090监听0） | WebView2只按应用进程树释放，不结束其他应用实例 |
| 队列预探测状态与插歌 | xUnit + TestServer + SignalR reducer + Vitest | 通过（Probing/ProbeFailed/Waiting 状态转换、访客权限、插歌排序、终态即时移除；Core 171/171、Web 28/28） | 100 首真实长队列和手机 WebSocket 待实机验收 |
| mpv 同实例切歌 | 生成 Unicode 双音轨 MKV + Windows Named Pipe JSON IPC | 通过（`stop` 产生 Stopped 事件；第二首加载及切歌前后 mpv PID 不变，2/2） | 电视输出切换观感待实机验收 |
| RC11 进程清理 | 全新解压启动、内嵌服务健康、窗口检测、正常关闭 | 通过（567 项；`PACKAGE_PROCESS_CLEANUP=passed`；SHA-256 `86200e1e…0552`） | WebView2 和真实 mpv 同时退出待实机观察 |
| SignalR 实时状态契约 | RoomRealtimeJournal JSON 契约测试 + TestServer/端到端 + Vitest reducer | 通过（camelCase、字符串枚举、旧字段兼容；API/端到端 8/8、Web 28/28、生产构建通过） | 真实手机 WebSocket 连接与断线重连待实机验收 |
| KTVS-073 播放控制回归 | Desktop xUnit + Release XAML + mpv Unicode named-pipe 外部测试 | 通过（Desktop 15/15；mpv 3/3；音量自动提交、空轨道标签和下一首解除暂停已覆盖） | 真实电视/功放待实机验收 |
| KTVS-074 队列生命周期回归 | Core xUnit + SQLite 唯一位置约束 + 队首播放存储 | 通过（Core 非外部 174/174；完成项不占限额/位置，ProbeFailed 队首可选取） | 真实 100 首长队列待实机验收 |
| KTVS-075 手机发现状态回归 | Vitest + TypeScript + Vite | 通过（Web 30/30；显式检索、歌星互斥卡片、菜单换行和标签切换状态持久化） | 真实手机 WebSocket 与视觉待实机验收 |
| KTVS-076 RC13 本地可执行包 | `scripts/publish-windows.ps1` + `scripts/verify-release-package.ps1` + `scripts/test-packaged-app.ps1` | 通过（579 项；自包含 .NET、mpv、FFmpeg/ffprobe/Vulkan、许可证说明；健康、窗口、正常退出和进程清理通过；SHA-256 `722415f966b38a2e5db3f629bcef6c0bc56a6c79e534a6d5fa8016dc7ae77889`） | 干净 Windows、真实媒体/设备和正式许可证复核待验收 |
| KTVS-077 按歌星默认卡片视图 | Vitest + TypeScript + Vite + RC14 package smoke | 通过（Web 31/31；主入口直接展示全部歌手卡片，二级歌手分组保持卡片视图并刷新 artistGroup；RC14 579 项，SHA-256 `e13b6733eb0f644e48f797c3b292f9c80d1bd9c8ddee118679998bb252531529`；健康、窗口和退出清理通过） | 真实手机小屏视觉与触控待实机验收 |
| KTVS-078 手机/WPF 插歌回归 | Core xUnit + Desktop xUnit + Web Vitest + Release 构建 + RC15 package smoke | 通过（Core 175/175、Desktop 16/16、Web 31/31；终态位置冲突、WPF 异常保护、手机队列更新覆盖；RC15 579 项，SHA-256 `24ad98043160892eb59547c64b4cb53d4178625a5bb5bc8edafdbfb45d11ed04`；健康、窗口和退出清理通过） | 真实手机操作、100 首长队列和 mpv 播放衔接待实机验收 |
| KTVS-079 歌手/桌面点歌/探测与播放交互 | Core/Desktop/Web + mpv/ffprobe 脚本 + RC16 smoke | 通过（Core 非外部 174/174、Desktop 16/16、Web 31/31、ffprobe 1/1、mpv 3/3、耐久 20/20；RC16 579 项，SHA-256 `be529461211d80578442849d88b7ac11d88714e034f284471ac13650e4e9b48e`；包、窗口、健康与退出清理通过） | 韩国歌手数据、真实头像、NAS 失败媒体、mpv 主动关闭与 WPF 视觉待实机验收 |
| KTVS-080 桌面点歌渲染性能 | Release 单节点构建 + Desktop xUnit + Core 非外部 xUnit + RC17 package smoke | 通过（Release 0 警告/错误；Desktop 17/17，含无变化队列项不重建断言；Core 非外部 174/174；RC17 579 项、SHA-256 `10a95842473b3d0965ef095d46a35765bca5b2e13005ee712e5320f6c495f0ea`，包、窗口、健康与退出清理通过） | 当前自动化范围没有可控制的 Station 窗口；真实大歌手库、远程头像时延和鼠标手感待 RC17 实机验收 |
| KTVS-081 至 KTVS-086 账户/歌单、桌面体验与队列语义 | SQLite migration + Core xUnit + Desktop xUnit + Vitest + Release build + format | 通过（追加式账户/歌单迁移、ProfileLibrary 2 场景、严格歌手下钻、当前播放后插播；Release 0 警告/错误，Core 非外部 178/178、Desktop 17/17、Web 31/31、format verify） | 真实家庭成员切换、远程头像、3,000 歌手鼠标手感、手机长连接及 mpv/长队列待验收 |
| RC18 本地验收包 | `publish-windows` + ZIP 校验 + 隔离启动/关闭 | 通过（579 项、296,718,483 bytes、SHA-256 `3b4f9f5ea29544159783f2353f373806ada39c677259f097c008089104b079c1`；健康、窗口和退出清理通过） | 干净 Windows、真实媒体/设备与长期内存观察待验收 |
| KTVS-087 / RC21 含初始库候选包 | SQLite Backup API 快照 + 预迁移/FTS 重建 + ZIP 单库校验 + 隔离启动/关闭 | 通过（580 项、353,069,922 bytes、SHA-256 `8af1d98e4893facb73871cc2d893f1f7e6cae618b3e087ace728872acdc7e8d8`；仅 `data/station.db`，无 WAL/SHM；健康、窗口和退出清理通过） | 源库与包含真实曲库数据，仅限本地验收；干净 Windows、真实媒体/设备与长期内存观察待验收 |
| KTVS-088 桌面作用域与歌手浏览缓存 | Core/桌面 xUnit + RC21 数据库只读快照基准 | 通过（Core 178/178、Desktop 18/18；72,295 首库 SQL 冷加载 672.03ms，缓存查询 P50 0.213ms/P95 0.370ms；并发加载合并、写入失效、换盘增量导入覆盖） | 真实 WPF 鼠标手感、内存和目标电脑盘符切换待验收 |
| KTVS-089 Server 管理与托盘启动器 | xUnit：嵌入 Kestrel 管理健康路由/DI，托盘运行时和播放器替身 | 通过（3/3：loopback 管理健康、服务注册、启动/Edge 失败回退/受控停止） | Windows 通知区、真实 Edge App 与 mpv 子进程退出待候选包实机验收 |
| RC22 WPF 收尾本地验收包 | 自包含发布 + SQLite 只读快照 + ZIP/隔离启动/退出 | 通过（580 项、353,069,531 bytes、SHA-256 `6483940b5cd42a4ebc91263041a35c7a3228908fa7eca04671e6d5afa4b3edf4`；仅 `data/station.db`，无 WAL/SHM；健康、窗口与退出清理通过） | 包含真实曲库数据，仅本地验收；真实 WPF 手感、挂载映射和设备待验收 |

| KTVS-091 Demo 桌面 Web 主控页 | Vitest + TypeScript + Vite production build + Server embedded host + Desktop/Core 回归 + RC32 package smoke | 通过（Web 34/34、TypeScript、Vite、Server/Tray Release、嵌入宿主 `1/1`、Desktop 18/18、Core 非外部 181/181、RC32 冷启动/托盘生命周期/进程清理） | 真实 Edge App 视觉、触控、局域网手机、长列表与实际媒体待验收 |
| KTVS-092 Demo 桌面播控闭环 | Vitest + TypeScript + Vite production build | 通过（Web 35/35；进度 `TimeSpan` 换算、音量防抖、进度跳转、音轨和字幕 API 控制） | 真实 Edge App、触控、局域网手机和实际媒体待验收 |
| KTVS-093 Demo 歌手卡片分页性能 | Vitest + TypeScript + Vite production build | 通过（Web 36/36；25 位歌手数据分页为 24/1 张 `.ktv-artist`，避免一次性创建整批卡片） | 真实 Edge App、触控和超大歌手库待验收 |
| KTVS-094 Demo 可见性、导航与连续播放修复 | Web Vitest + TypeScript + Vite + Server Release build + Core 非外部 + mpv 外部夹具 | 通过（Web 38/38、TypeScript、Server 0 警告/0 错误、Core 181/181、mpv 外部 3/3；覆盖按钮可见基线、导航反馈、桌面点歌 POST、替换事件后仍保持 Playing） | 真实 Edge App、触控和长时间连续播放待验收 |
| KTVS-095 Demo 深色视觉与桌面导航内页 | Web Vitest + TypeScript + Vite production build | 通过（Web 38/38、TypeScript、生产构建；覆盖深色 CSS 基线、总览/播放/队列/曲库/房间/设置内页切换和返回点歌） | 真实 Edge App、触控和长列表视觉待验收 |
| KTVS-096 RC36 复盘后的桌面 Web 主控重构与 RC39 包 | Core/Desktop xUnit + Web Vitest + TypeScript + Vite production build + Server Release + package verify/smoke | 通过（Core 181/181、Desktop 18/18、Web 38/38、Server 0 警告/0 错误、RC39 451 项；使用指定 seed 数据库生成单个 `data/station.db`，覆盖宽屏点歌、显式歌名/歌手搜索、播放/队列/二维码/曲库统计/设置内页与管理接口，托盘冷启动/退出清理通过） | 真实 Edge App、手机、媒体和硬件待验收 |
| KTVS-097 连续播放事件隔离、重复进度条与二维码投屏 | Core/Desktop xUnit + Web Vitest + TypeScript + Vite production build + Server Release + mpv external | 通过（Core 非外部 189/189、Desktop 18/18、Web 38/38、Server 0 警告/0 错误、mpv 外部 4/4；覆盖快速连续换歌、`Replaced` 事件不推进队列、单进度滑块、loopback QR overlay API） | 真实电视画面、不同 mpv 构建、长队列和实际扫码待实机验收 |
| KTVS-098 RC40 自包含托盘发布包 | `publish-windows.ps1` + `verify-release-package.ps1` + `test-packaged-app.ps1` | 通过（451 项，339,407,106 bytes，SHA-256 `aa3c19b4e961726b2d17adfd5961d138e110379bfea3340cc7566abc25597e63`；含 mpv/FFmpeg/ffprobe/Vulkan loader、许可证、单个 `data/station.db`；发布校验、健康、冷启动待机、托盘生命周期和进程清理通过） | 指定外部数据库路径当前不存在，使用 RC39 已验收数据库快照；真实 Windows/媒体/设备仍待用户验收 |
| KTVS-099 播放诊断与桌面 Toast 反馈 | Core xUnit + Server embedded host + Web Vitest + TypeScript + Vite production build | 通过（Core 非外部 191/191、Web 39/39、Server/Web 编译与生产构建通过；覆盖非用户主动 `end-file` 原因入库、主动切歌不记失败、管理接口读取和播放器页展示） | 真实 mpv 原因分布、长队列连续播放和电视设备仍待实机验收 |

禁止把替身、模拟器或文档审阅结果写成真实设备通过。
