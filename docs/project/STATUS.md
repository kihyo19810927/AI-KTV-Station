# 项目状态

更新时间：2026-09-03

## 当前基线

- 分支：`task-KTVS-001-002-phase0`
- 版本：`0.1.0-dev`
- 阶段：Phase 2：数据库、扫描与搜索
- 已完成：KTVS-001 至 KTVS-014
- 当前任务：KTVS-015 ffprobe 媒体探测

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

## 外部阻塞

1. 真实 MKV、挂载目录和电视/功放/手机验收需用户后续执行，不阻塞软件开发。

## 用户实机验收（待验证）

尚未进行。后续需用户在 Windows 11 上确认：mpv 画面、双音轨语义、字幕、手机扫码入房、CloudDrive 挂载速度及音频输出。

## 下一推荐任务

执行 `KTVS-015`：用 ffprobe 探测生成 MKV 的时长、视频/音频/字幕轨及错误分类。
