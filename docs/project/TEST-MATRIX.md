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
| Host/API/SignalR | 集成测试 | 未开始 | .NET SDK |
| mpv IPC | 生成夹具 + Windows named-pipe 实机进程 | 软件 Spike 通过 | 真实 MKV/设备待验收 |
| ffprobe 解析 | 固定 JSON 样本 | 未开始 | 无（实现阶段） |
| React Web | npm build/test | 未开始 | Web 工程尚未建立 |
| 环境与生成 MKV | `scripts/verify-environment.ps1` | 通过（10.023s、4 轨、中文标题） | 无 |
| 真实 MKV/设备 | `ENVIRONMENT-BASELINE.md` 用户验收步骤 | 待验证 | 用户设备、样本和挂载 |

禁止把替身、模拟器或文档审阅结果写成真实设备通过。
