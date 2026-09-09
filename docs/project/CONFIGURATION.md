# 配置与日志基线

`StationOptions` 是 `Station` 配置节的唯一强类型入口。默认仅绑定 `127.0.0.1:5090`，数据目录使用相对路径 `data`，mpv 路径为空并在部署环境显式提供。启动时验证端口、目录和播放器命令超时；无效配置阻止服务启动。

仓库内配置只能包含无敏感默认值。真实媒体路径、数据库位置、令牌、Cookie 和凭据不得提交。日志使用 JSON Console 基线，进入日志属性前应通过 `SensitiveDataRedactor` 对名称含 `password`、`secret`、`token`、`cookie`、`authorization` 或 `path` 的值脱敏。

应用错误使用稳定错误代码和面向用户的安全消息。底层异常、路径与原始 mpv/SQLite 错误只进入受控诊断上下文，不通过公共 DTO 暴露。

Windows 主控从当前用户的 `%LocalAppData%\AI-KTV Station\settings.json` 读取覆盖值。设置页可修改监听地址、端口、数据目录、mpv 可执行文件和 IPC 命令超时；保存前复用 `StationOptionsValidator`，变更在重启主控后生效。该文件属于主机私有状态，不应加入仓库。

本地事件日志位于同一用户目录下的 `logs\station.jsonl`，仅写入受控事件代码与不含路径、令牌或底层异常的消息。单条字段限制长度并移除换行；文件超过 1 MiB 时保留最近 1000 条。诊断导出仅包含运行时摘要、路径是否配置、健康摘要、恢复建议和最近 200 条受控事件，不包含实际媒体路径或播放器路径。
