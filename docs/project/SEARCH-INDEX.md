# SQLite FTS5 搜索索引

KTVS-018 按 [ADR-0008](../adr/ADR-0008-fts5-search-index.md) 建立只含公开字段的 `SongSearchDocuments`，并由 SQLite trigger 原子同步 external-content `SongSearchFts`。搜索结果不查询或返回媒体路径。

`ISongSearchIndex` 支持：

- 幂等全量 rebuild 和按歌曲 ID upsert/delete；
- 原文、简繁、全拼、简拼、紧凑标题及每位歌手搜索；
- 页码从 1 开始，页大小限制为 1～100；
- 相关度、标题、年份降序和最近新增四种稳定排序；最近新增取已索引媒体最新 `LastWriteTime`，以公开 `AddedAt` 返回；
- 语言、类别、清晰度和年份范围组合筛选；
- 空查询的筛选浏览，以及特殊 FTS 字符安全转义。

索引文档是可重建派生数据，`Song`/`Artist` 仍是事实来源。元数据事务提交后应调用 `UpsertAsync`；中断造成不一致时运行 `RebuildAsync`，不能通过删除歌曲索引“修复”。KTVS-019 扫描管理用例负责接入该同步端口。

## 100k 性能基线

命令：`pwsh.exe -ExecutionPolicy Bypass -File scripts/test-search-performance.ps1`

2026-09-07 本机结果：SQLite 临时数据库 100,000 条文档，预热后混合 15 次单曲精确拼音和 15 次约 200 命中的歌手拼音查询，共 30 个样本；P95 `4.74 ms`，最大 `5.03 ms`，低于计划的 `200 ms` 目标。数据生成和预热不计入查询样本。

该结果是当前开发主机上的合成基线，不代表 300k 数据、低规格 Windows 主机或真实使用体验；这些仍需后续发布矩阵复测。
