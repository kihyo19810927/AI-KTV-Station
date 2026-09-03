# 可执行 Backlog

状态：`Todo` / `In Progress` / `Blocked` / `Done`。任务依赖必须满足后才能开始。

## Phase 0

| ID | 标题 | 依赖 | 状态 | 验收标准 | 风险/用户参与 |
|---|---|---|---|---|---|
| KTVS-001 | 新仓库初始化与基线调查 | 无 | Done | 独立 Git、README、AGENTS、状态/测试/发布文档；记录工具链和阻塞 | 无；不覆盖用户文件 |
| KTVS-002 | 产品边界与领域词汇 | KTVS-001 | Done | CHARTER 含范围、角色、用例、术语和明确非目标 | 无 |
| KTVS-003 | 技术可行性 Spike | KTVS-001,002 | Done | C# 启动 mpv、读状态、枚举/切换音轨、切换字幕、接收结束事件；夹具可重复生成 | 真实 MKV/设备仍待用户验收 |
| KTVS-004 | 架构决策记录 | KTVS-002,003 | Done | 仓库边界、播放器、数据库、Web 构建和认证 ADR 已接受 | 无 |
| KTVS-005 | 实际环境基线 | KTVS-003,004 | Done | 自动环境与生成媒体基线通过；真实 MKV、挂载和设备结果有明确待验收清单 | 实机验收延期，不阻塞软件开发 |

## Phase 1

| ID | 标题 | 依赖 | 状态 | 验收标准 | 风险/用户参与 |
|---|---|---|---|---|---|
| KTVS-006 | 解决方案和项目依赖方向 | KTVS-004 | Done | Domain/Application/Infrastructure/Server/Desktop/Web 骨架可构建，依赖方向自动检查 | 无 |
| KTVS-007 | 配置、日志和错误模型 | KTVS-006 | Done | 强类型配置、JSON 日志/脱敏边界、统一 Result/Error 有测试 | 无敏感默认值 |
| KTVS-008 | 测试基线 | KTVS-006 | Done | xUnit、测试数据工厂、覆盖率命令可执行，生成物不入库 | NuGet 依赖已锁定 |
| KTVS-009 | CI 基线 | KTVS-006,008 | Done | locked restore/build/test/覆盖率/格式检查工作流；Web 构建入口明确 | GitHub runner 待首次推送后确认 |
| KTVS-010 | 本地开发脚本 | KTVS-006,008 | Done | bootstrap/build/test/run/smoke 命令文档化并在本机通过 | 无 |

## Phase 2

| ID | 标题 | 依赖 | 状态 | 验收标准 | 风险/用户参与 |
|---|---|---|---|---|---|
| KTVS-011 | 领域模型与 SQLite Schema | KTVS-010 | Done | 14 个首版实体、EF Core 映射、InitialCreate 迁移和 SQLite 集成测试 | 无 |
| KTVS-012 | MediaSource 管理 | KTVS-011 | Done | 多根目录、启停、只读路径验证；公共 DTO 无真实路径 | 测试使用临时目录 |
| KTVS-013 | 文件枚举与增量扫描 | KTVS-012 | Done | 可取消、检查点、错误隔离、增量更新；离线不删索引 | 真实挂载待验收 |
| KTVS-014 | 文件名解析器 | KTVS-011 | In Progress | 容错规则和脱敏样本回归测试 | 用户真实命名样本后补 |
| KTVS-015 | ffprobe 媒体探测 | KTVS-013 | Todo | 时长、轨道、编码信息与失败分类 | 生成 MKV 可自动测 |
| KTVS-016 | NFO 可选读取 | KTVS-011 | Todo | 不一致处理、人工字段优先级 | 无 |
| KTVS-017 | 搜索规范化 | KTVS-011 | Todo | 拼音、简拼、原文规范化有 Unicode 测试 | 无 |
| KTVS-018 | FTS 搜索与筛选 | KTVS-011,017 | Todo | 分页/排序/筛选及 100k 性能基线 | 无 |
| KTVS-019 | 扫描管理 API | KTVS-013,015 | Todo | 创建、取消、进度、结果 API | 无 |

## 后续阶段门禁

- Phase 0 软件基线完成；真实 MKV/设备门禁仍为待实机验收。按持续开发约定可进入非硬件软件任务，但不得把实机项标为通过。
- Phase 1 门禁：全新克隆一条命令构建、测试和 Web 构建通过。
- Phase 1 软件门禁：bootstrap/build/test/server smoke 已通过；正式 Web build 随 KTVS-034 完成，GitHub runner 待首次推送验证。
- Phase 2 门禁：真实脱敏曲库扫描、错误报告、100k 搜索性能，且不修改媒体。
- Phase 3 及以后：详见权威开发计划第 15 节；每个任务完成前同步 STATUS、测试和文档。
