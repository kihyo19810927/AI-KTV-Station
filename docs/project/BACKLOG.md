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
| KTVS-009 | CI 基线 | KTVS-006,008 | In Progress | restore/build/test/Web build/格式检查工作流 | CI 云端运行待首次推送后确认 |
| KTVS-010 | 本地开发脚本 | KTVS-006,008 | Todo | bootstrap/build/test/run 命令文档化并在本机通过 | 无 |

## 后续阶段门禁

- Phase 0 软件基线完成；真实 MKV/设备门禁仍为待实机验收。按持续开发约定可进入非硬件软件任务，但不得把实机项标为通过。
- Phase 1 门禁：全新克隆一条命令构建、测试和 Web 构建通过。
- Phase 2 门禁：真实脱敏曲库扫描、错误报告、100k 搜索性能，且不修改媒体。
- Phase 3 及以后：详见权威开发计划第 15 节；每个任务完成前同步 STATUS、测试和文档。
