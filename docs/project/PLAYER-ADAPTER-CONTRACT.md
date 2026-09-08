# 播放器适配器契约

KTVS-020 将所有播放业务限定在 Application 的 `IPlayerAdapter`。接口提供启动、停止、加载、播放、暂停、定位、音量、音轨、字幕、状态读取和异步事件流；Infrastructure 的 mpv 实现不得把 JSON IPC、named pipe 或进程类型泄漏到该边界之外。

## 状态和关联标识

生命周期状态为 `Stopped`、`Idle`、`Preparing`、`Playing`、`Paused`、`Ended`、`Failed`。每次加载都要求非空 `PlaybackId`，状态和所有媒体事件携带该 ID，使后续队列编排能够丢弃旧播放实例的迟到事件。每个事件还有独立 `EventId`，用于幂等去重。

`PlayerSnapshot` 包含位置、时长、0～100 音量、当前音频/字幕流和公开轨道描述。媒体真实路径只存在于主机内部命令 `PlayerLoadRequest`，不会进入 snapshot、事件、失败对象、Web API 或普通日志。

## 结束和错误语义

`PlaybackEndedEvent` 明确区分自然完成、主动停止、被新媒体替换和失败，避免把“切歌”误当作自然结束。`PlayerFailure` 提供稳定错误码、分类、是否可重试和可公开消息，不包含原始 mpv JSON、命令行、pipe 名或文件路径。

命令取消使用调用方 `CancellationToken`；适配器不得把调用取消伪装成播放器故障。音量、位置、轨道和加载请求具有独立参数验证规则。KTVS-021 负责将 mpv 进程/IPC 映射到该契约，KTVS-022 再实现业务状态机。
