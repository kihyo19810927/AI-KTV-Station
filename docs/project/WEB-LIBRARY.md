# Web 收藏与发现

KTVS-039 完成确认 Demo 的推荐、热门、最近新增和收藏交互。

## 发现页

- “推荐”沿用可搜索、筛选、分页的曲库结果。
- “热门”读取 `GET /api/library/popular?take=20`，排名只基于本房间成功播放历史。
- “最近新增”使用搜索排序 `RecentlyAdded`。公开 `AddedAt` 取歌曲所有索引媒体的最新 `LastWriteTime`；它表示曲库发现到的最近媒体版本时间，不是云盘上传审计时间。
- 心形按钮通过 `PUT /api/library/favorites/{songId}` 幂等收藏/取消，初始状态来自当前访客收藏列表。

## 收藏页

收藏按当前访客隔离，显示歌名和歌手，支持直接点歌与取消收藏。所有写操作仍由服务端验证短期令牌、歌曲存在性和队列规则。

## 索引迁移

`SongSearchDocuments.AddedAt` 是可空 ISO-8601 文本字段；旧索引迁移后值为空，在下次扫描 upsert 或索引 rebuild 时从媒体 `LastWriteTime` 填充。排序使用 `AddedAt DESC` 并以规范歌名和歌曲 ID 保证稳定性。字段不包含路径。
