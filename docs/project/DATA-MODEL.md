# 数据模型基线

KTVS-011 建立 14 个核心实体：`Song`、`Artist`、`SongArtist`、`MediaSource`、`MediaFile`、`MediaTrack`、`TrackMapping`、`RoomSession`、`Guest`、`QueueItem`、`Favorite`、`PlayHistory`、`ScanRun`、`PlaybackError`。

关键约束：

- `MediaSourceId + RelativePath` 唯一，文件离线只改变可用状态。
- Song 与媒体文件分离；一个 Song 可对应多个媒体文件。
- Guest 只持久化令牌哈希，不保存明文令牌。
- 房间内队列位置唯一；状态按字符串持久化，便于诊断和兼容迁移。
- Song/Artist 规范化字段建立索引；FTS5 在 KTVS-018 增加。
- EF Core 仅存在于 Infrastructure；Domain 模型不含 EF 特性或文件系统调用。

初始迁移为 `InitialCreate`。迁移集成测试使用随机临时 SQLite 文件并关闭连接池，测试后只删除该测试创建的文件。
