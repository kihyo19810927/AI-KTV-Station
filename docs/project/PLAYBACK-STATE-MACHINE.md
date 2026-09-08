# 播放状态机

KTVS-022 在 Application 层提供与播放器实现无关的 `PlaybackStateMachine`。适配器负责报告技术事实，状态机负责接受合法生命周期迁移，并为后续队列编排提供单调递增的业务状态。

状态机以 `PlaybackId` 识别当前播放实例：开始加载新媒体后，旧实例的结束或失败事件标记为 `Stale`，不会推进新歌曲。`EventId` 在容量受限的窗口中去重，重复事件标记为 `Duplicate`；同一播放实例中早于最近已应用事件时间的乱序事件也标记为 `Stale`。非法生命周期迁移返回稳定错误 `player.invalid_transition` 且不修改状态。

合法主路径为 `Stopped → Idle → Preparing → Playing ↔ Paused → Ended`；主动停止和故障迁移可从活动状态进入 `Stopped` 或 `Failed`，结束/故障后允许加载下一首。状态机仅依赖 Application 的播放器契约，不引用 Infrastructure、mpv、UI、Web 或文件系统。
