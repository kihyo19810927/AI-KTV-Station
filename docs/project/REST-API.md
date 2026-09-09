# REST API v1

KTVS-031 的 Minimal API 契约由 `/openapi/v1.json` 发布，统一失败响应为 RFC 7807 `application/problem+json`，并在扩展字段 `code` 返回稳定错误码。

主要端点：

- 本机管理：`POST /api/rooms`、`GET /api/rooms/current`；创建房间同时一次性返回主持人短期令牌。
- 房间：`POST /api/rooms/join`、关闭房间、撤销成员。
- 曲库：`GET /api/catalog/search`，支持文本、分页、筛选和排序。
- 队列：查询、点歌、删除、主持人置顶。
- 播放：状态、播放/暂停、音量、定位、音轨和字幕。
- 扫描：创建、进度、结果和取消，沿用相同错误格式。

除健康检查、访客加入和本机房间创建/读取外，端点均要求 `Authorization: Bearer <短期房间令牌>`。所有变更在服务端验证房间、有效期、撤销和角色权限。本机管理入口依据连接来源限制为 loopback；不自动开放防火墙或路由器。

公开 DTO 与 OpenAPI 文档自动检查不包含 `RootPath`、`RelativePath` 或 `TokenHash`。真实令牌不得写入日志；手机二维码与局域网地址组合在后续 Web/WPF 任务实现。
