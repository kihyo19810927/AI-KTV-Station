# MediaSource 管理

`MediaSourceService` 提供添加、多源列表和启停。文件系统检查通过 `IMediaPathInspector` 端口隔离，实际实现只执行规范化、存在性与最小可读性检查，不创建、移动、重命名或删除媒体。

公共 `MediaSourceSummary` 只包含 ID、名称、启用状态和可用性；真实根路径仅存在于主机管理员用 `MediaSourceAdminDetails`。服务拒绝缺失、不可读和规范化后重复的根目录。

路径检查不代表 CloudDrive 健康或媒体可播放；扫描和播放阶段必须分别处理离线、超时和访问失败，并保留数据库索引。
