# 播放故障恢复

KTVS-025 将单次播放器故障和业务恢复策略分离。`PlaybackRecoveryPolicy` 根据稳定错误分类产生 `RetryCurrent`、`SkipCurrent` 或 `HaltPlayback`，默认单曲最多重试两次，退避为 250ms、500ms 且有 2 秒上限。进程退出、连接/命令超时和协议错误要求在重试前重启播放器。

媒体离线、加载失败等可恢复错误先重试当前歌曲；命令、连接或协议超时耗尽次数后停止自动推进，避免通信故障连续跳过整个队列。可明确判定为不支持或不可恢复的媒体才跳过。mpv 缺失、启动失败、非法参数或状态属于主机配置问题，同样停止播放编排。

`PlaybackRecoveryService` 在返回决定前记录 `PlaybackError`。可重试的 `MediaLoadFailed` 只记录错误，不把媒体永久标成不可读；来源在线时，历史 `Unreadable` 媒体仍允许后续重试。`EfPlaybackFailureStore` 保留歌曲、媒体索引和错误历史。

队列的 ffprobe 预探测是建议性检查：三次失败会写入 `LastErrorCode` 和本机诊断日志，但队列仍进入 `Waiting` 并由 mpv 实际加载。日志记录 ffprobe 退出码、精简 stderr、耗时和安全文件名，以及 mpv `end-file` 的错误字段与播放列表代次，不写入完整媒体路径。模拟 403、断挂、命令超时与恢复由 KTVS-050 覆盖；真实 115/CloudDrive 错误映射和恢复时延仍待实机验收。
