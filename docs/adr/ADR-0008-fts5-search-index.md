# ADR-0008：FTS5 搜索索引与同步

状态：Accepted

日期：2026-09-07

## 背景

V1 需要在 100,000～300,000 首歌曲下按歌名、歌手、拼音、简拼、语言和类别检索，代表性主机普通查询 P95 目标小于 200 ms。查询不能触碰 CloudDrive 媒体目录，API 也不能泄漏真实路径。

## 决策

- SQLite 内建立普通表 `SongSearchDocuments` 保存公开显示字段、筛选字段和合并后的搜索词，再以 external-content FTS5 表 `SongSearchFts` 索引 `Terms`。
- SQLite trigger 保证文档表插入、更新、删除与 FTS 索引原子同步；应用通过 `ISongSearchIndex` 端口执行单曲 upsert 或全量 rebuild。
- 搜索结果只来自搜索文档，不包含 `MediaFile` 或 `MediaSource.RootPath`。查询字符串先经 KTVS-017 的规范化器生成原文、简繁、拼音、简拼和紧凑候选，再编译为参数化 FTS 前缀查询。
- 支持限定页大小、稳定分页、相关度/标题/年份排序，以及语言、类别、清晰度和年份范围筛选。空查询用于筛选浏览，不调用 `MATCH`。
- 100k 合成数据基线作为独立 `Performance` 测试运行；默认快速测试排除该 trait，但 CI/发布门禁必须显式执行性能脚本并记录 P95。

## 后果

读路径与 EF 实体图解耦，查询稳定且不访问媒体。歌曲或歌手元数据更新后必须 upsert；扫描管理用例在提交数据库事务后调用索引端口。若进程在两步之间退出，可运行幂等 rebuild 修复，不删除歌曲事实数据。FTS5 是否可用在迁移集成测试中验证。
