# 扫描管理 API

KTVS-019 提供后台扫描的创建、取消、进度和结果读取。后台任务由单例协调器登记，但每次执行都创建独立 DI scope，因此不会持有已结束的 HTTP request scope 或共享 `DbContext`。

## 端点

| 方法与路径 | 行为 |
|---|---|
| `POST /api/scans` | JSON `{ "mediaSourceId": "..." }`；返回 `202` 和操作地址 |
| `GET /api/scans/{scanRunId}` | 返回当前进度；进程重启后可回读持久化记录 |
| `GET /api/scans/{scanRunId}/result` | 运行中返回 `202`，终态返回 `200` |
| `POST /api/scans/{scanRunId}/cancel` | 只取消指定的当前进程内操作；返回 `202` |

同一媒体源不能并发启动两个扫描。响应只包含运行 ID、媒体源 ID、状态、时间和计数，不包含 checkpoint 相对路径、媒体根路径、完整文件路径或诊断异常文本。错误使用稳定 `code`，不存在为 `404`，重复运行或已结束操作为 `409`。

扫描器持续上报发现、更新和错误计数；取消状态及 checkpoint 仍写入 SQLite。完成扫描提交数据库后，协调器 rebuild 可派生 FTS 文档，进程在数据库提交与 rebuild 之间退出时可再次 rebuild，不删除歌曲事实数据。

当前 API 由 Server 组合根在启动时迁移 SQLite，默认数据库位于配置的数据目录。房间/管理员认证将在 Phase 5 接入；在此之前默认绑定仍为 loopback，禁止把当前开发服务开放到公网。
