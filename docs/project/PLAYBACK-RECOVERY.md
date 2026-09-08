# 播放故障恢复

KTVS-025 将单次播放器故障和业务恢复策略分离。`PlaybackRecoveryPolicy` 根据稳定错误分类产生 `RetryCurrent`、`SkipCurrent` 或 `HaltPlayback`，默认单曲最多重试两次，退避为 250ms、500ms 且有 2 秒上限。进程退出、连接/命令超时和协议错误要求在重试前重启播放器。

媒体离线、加载失败等可恢复错误耗尽次数后自动跳过当前歌曲；不支持媒体直接跳过。mpv 缺失、启动失败、非法参数或状态属于主机配置问题，停止播放编排，避免整列歌曲重复失败。

`PlaybackRecoveryService` 在返回决定前记录 `PlaybackError`。诊断摘要仅由错误分类和恢复动作组成，不记录媒体路径或原始 IPC。`EfPlaybackFailureStore` 对临时不可用媒体只更新 `Availability`/`LastErrorCode`，保留歌曲、媒体索引和错误历史。真实 115/CloudDrive 断挂、403 和恢复时延待实机验收。
